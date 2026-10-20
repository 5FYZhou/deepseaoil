using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Actor
{
    public enum EnemyAIType
    {
        Chase = 0,
        Stand = 1,
    }

    /// 一只敌人的组合根：推进逻辑层，结算伤害与死亡
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour, IDamageable, ISlowable, IKnockBackable, IStunnable, IManagedActor, IContactDamager, IYSortAnchor, IAggroBreakable, IEnemyBody
    {
        // TintStep：肉眼分不出的倍率差；火花节拍与并发上限压在 ChainShock 池 1/16 以内
        private const float TintStep = 0.05f;

        private const float SparkInterval = 0.3f;

        private const int SparkCapacity = 8;

        private static int _sparkActive;

        [Header("死亡反馈")]
        [Tooltip("死亡时播放的特效；由预制体配置")]
        [SerializeField] private EffectId deathEffect = EffectId.Shatter;

        [Header("AI 与打靶")]
        [Tooltip("enemy 表的 id：只对场景摆放的木桩生效（波次怪的 id 由 wave_enemy.enemy_id 定）；填错只会用到别的怪种的数值，不报错")]
        [SerializeField] private int enemyId = 1;

        [Tooltip("AI行为模式：Chase 正常追人，Stand 原地站桩木桩；只对场景木桩生效，波次怪恒为 Chase")]
        [SerializeField] private EnemyAIType aiType = EnemyAIType.Chase;

        [Tooltip("打靶复活延迟（秒）：<=0 正常死亡销毁，>0 原地复活供无限打靶测试；只对场景木桩生效")]
        [SerializeField] private float respawnDelay = 0f;

        private Vector2 _spawnPosition;

        private Coroutine _respawnRoutine;

        private Collider2D _collider;

        public bool IsInitialized => Stats != null;

        /// 吃 Inspector 打靶配置，清场归位
        public bool IsScenePlaced { get; set; }

        /// 本预制体对应 enemy 表的哪一行；场景接管路径读它，波次路径用注入的 spec
        public int EnemyId => enemyId;

        private EnemySpec _spec;
        private EnemyLogic _logic;
        private EnemyMotor _motor;

        /// 视觉适配器（挂在子节点 View 上）
        private ActorAnimationView _animView;

        /// 身体渲染器（受击暂停与复活时开关显隐），来自 View 的 SpriteRenderer
        private SpriteRenderer _body;

        private Transform _target;
        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        private bool _registered;
        private Vector3Int _currentCell;

        /// 格子系统未接线只报一次：逐帧报会刷爆 Console，不报则静默
        private bool _gridWiringWarned;

        private float _slowMultiplier = 1f;

        private float _hitStopUntil;

        /// 贴身伤害，取自 enemy 表；ContactProbe 经 IContactDamager 读
        public int ContactDamage => _spec != null ? _spec.ContactDamage : 0;

        /// 死亡回调，只触发一次
        public System.Action<EnemyActor> Died;

        public System.Action<AudioId> SfxRequested;

        private bool _deathFired;

        /// 减速期间持有，离开减速格 Stop
        private EffectHandle _slowTintHandle;

        /// 尸体淡出句柄：复活前必须 Stop
        private EffectHandle _fadeHandle;

        private bool _deathDone;

        private float _tintScale = 1f;

        private bool _sparkOn;

        private float _sparkTimer;

        public bool IsAlive => Stats != null && Stats.IsAlive;

        /// 取执行器物理体位置而非 transform.position，两者迟早有一帧对不上且不报错
        public Vector2 Position => _motor != null ? _motor.Position : (Vector2)transform.position;

        public float Radius => _spec != null ? _spec.Radius : 0f;

        Vector2 IYSortAnchor.SortAnchor => Position;

        public EnemyStats Stats { get; private set; }

        public int Hp => Stats != null ? Stats.Hp : 0;

        public bool IsHurt => Stats != null && Stats.IsAlive && _logic != null && _logic.IsHurt;

        public Vector2 EngineVelocity => _motor == null ? Vector2.zero : _motor.EngineVelocity;

        /// 诊断面板读数
        public float SlowMultiplier => _slowMultiplier;

        private void Awake()
        {
            BindPrefabParts();
        }

        /// 补空不覆盖：Awake 与 Initialize 都会调，编辑器装配路径不跑 Awake
        private void BindPrefabParts()
        {
            if (_motor == null) _motor = GetComponent<EnemyMotor>();

            if (_animView == null) _animView = GetComponentInChildren<ActorAnimationView>(true);

            if (_body == null)
            {
                _body = _animView != null && _animView.Renderer != null
                    ? _animView.Renderer
                    : GetComponentInChildren<SpriteRenderer>(true);
            }

            if (_collider == null) _collider = GetComponent<Collider2D>();
        }

        /// 组装一只敌人，依赖全由参数给出；target=null 则滑停，registry=null 则不登记
        /// IsScenePlaced 为真才吃 Inspector 打靶配置与复活；权属由 CombatRoot 的接管入口置位
        public void Initialize(
            Vector2 position,
            in EnemySpec spec,
            Transform target,
            Vector2 facing,
            GridLogic grid,
            EnemyCellRegistry registry,
            Transform parent)
        {
            BindPrefabParts();

            if (_motor == null)
            {
                Debug.LogError(
                    $"[Enemy] 预制体根节点没有 EnemyMotor（它 [RequireComponent(Rigidbody2D)]）：{name} 生成已作废。" +
                    "请检查 Assets/Resources/enemies/Enemy_*.prefab 的 Root 组件。",
                    this);

                Destroy(gameObject);

                return;
            }

            _spec = spec;
            _target = target;
            _grid = grid;
            _registry = registry;

            Stats = new EnemyStats(spec);

            transform.position = new Vector3(position.x, position.y, 0f);

            if (parent != null) transform.SetParent(parent, true);

            // 建完刚体立刻固化物理参数，否则首次读位置前的物理步用预制体旧值跑
            _motor.EnsureInitialized();

            _spawnPosition = position;

            IEnemyBrain brain = IsScenePlaced && aiType == EnemyAIType.Stand
                ? EnemyBrainFactory.CreateStand()
                : EnemyBrainFactory.Create(spec);

            _logic = new EnemyLogic(_motor, spec, brain);

            _logic.AttachGrid(_grid);

            _motor.Facing = facing;

            UpdateCell(force: true);

        }

        /// Play 模式下拖入的怪由此报到：靠 Unity 原生生命周期，不轮询场景树
        private void Start()
        {
            if (!Application.isPlaying || IsInitialized) return;

            CombatRoot combat = CombatRoot.Current;

            if (combat != null) combat.AdoptSceneEnemy(this);
        }

        /// 唯一受伤入口；格子状态转换、近战与陷阱都走这里
        public void TakeDamage(in Damage damage)
        {
            if (Stats == null || !Stats.IsAlive) return;

            if (damage.HasDamage)
            {
                SfxRequested?.Invoke(AudioId.EnemtHit);

                VisualFeelTuning feel = ConfigModule.VisualFeel;

                // 耐久是整数，伤害舍入到整数
                Stats.ApplyDamage(damage.Amount);

                // Anchor 给驱动，真身渲染器由驱动自己下探（跳过 ShadowView）
                EffectContext flash = EffectContext.OnTarget(transform);
                flash.Duration = feel.flashDuration;
                flash.Tint = Color.white;

                EffectModule.Play(EffectId.Flash, in flash);

                // 元素序号由 Logic 定，数据层按序号查色
                EffectContext popup = EffectContext.At(Position);
                popup.Value = damage.Amount;
                popup.Tint = ConfigModule.Visuals.DamageColor((int)damage.Element);

                EffectModule.Play(EffectId.DamagePopup, in popup);

                // 写 View 不写 Root：Root.localScale.x 是朝向镜像，叠乘会变胖；旋钮在 EnemyTuning 行
                if (_spec != null && _spec.Squash.amount > 0f)
                {
                    SquashTuning squash = _spec.Squash;

                    // 条目填 0 视为未填：回落全局观感，0 时长会变成看不见的形变
                    float duration = Stats.IsAlive
                        ? (squash.normalDuration > 0f ? squash.normalDuration : feel.squashDuration)
                        : (squash.deathDuration > 0f ? squash.deathDuration : feel.squashDurationLethal);

                    float cap = squash.maxAmount > 0f ? squash.maxAmount : feel.squashIntensity;

                    EffectContext punch = EffectContext.OnTarget(
                        _animView != null ? _animView.transform : transform);
                    punch.Duration = duration;
                    punch.Value = Mathf.Min(squash.amount, cap);
                    punch.Modulation = new Vector2(squash.compressRatio, squash.easePower);

                    EffectModule.Play(EffectId.Squash, in punch);
                }

                // 换 transform.position 会错开
                EffectContext spark = EffectContext.At(Position, damage.Direction);

                EffectModule.Play(EffectId.BurstSparks, in spark);

                BeginHitStop(feel.hitStopSeconds);
            }

            if (damage.HasKnockback && _logic != null)
            {
                // 只递交，下个逻辑帧才"进入受击"：v0 由本节拍位移与本节阻尼反解，与 HurtState 同源
                float targetDistance = damage.KnockbackCells * CellSize;

                float v0 = Mathf.Sqrt(2f * HurtDeceleration() * targetDistance);

                // 非数/无穷会被账本原样写进刚体，整段位移直接作废
                if (!float.IsNaN(v0) && !float.IsInfinity(v0))
                {
                    _logic.ApplyKnockback(v0, damage.Direction);
                    SfxRequested?.Invoke(AudioId.EnemyKnockback);
                }

                // 抬 View 不抬 Root：Y-Sort 读物理体位置
                if (_animView != null)
                {
                    EffectContext hop = EffectContext.OnTarget(_animView.transform);
                    hop.Value = v0 / ConfigModule.VisualFeel.hopReferenceSpeed;

                    EffectModule.Play(EffectId.Hop, in hop);
                }
            }

            if (!Stats.IsAlive)
            {
                Die(damage.Direction);
                return;
            }

            _animView?.TriggerHurt();
        }

        /// <summary>格边长（世界单位）；网格未接线时退化为 1f</summary>
        private float CellSize => _grid != null && _grid.Geometry.IsValid ? _grid.Geometry.CellSize : 1f;

        /// <summary>滑停减速度：hurtDecay 非法/未填即落回 moveAcceleration</summary>
        private float HurtDeceleration()
        {
            float decay = _spec != null ? _spec.Config.hurtDecay : 0f;

            if (decay > 0f && !float.IsNaN(decay) && !float.IsInfinity(decay)) return decay;

            float accel = _spec != null ? _spec.Config.moveAcceleration : 0f;

            return accel > 0f && !float.IsNaN(accel) ? accel : 10f;
        }

        /// 由 CombatRoot 每物理帧调：速度一帧只提交一次，视效同频刷新
        public void FixedTick(float now, float deltaTime)
        {
            if (!Stats.IsAlive) return;

            _logic.SetTarget(_target == null ? (Vector2?)null : TargetPosition());
            _logic.Tick(now, deltaTime);

            _slowMultiplier = _logic.Status.SlowScale;

            ApplySlowTint();
            TickStunSpark(deltaTime);
            UpdateCell(force: false);
            RestoreHitStop(now);

            // 运动学快照与移动同频提交：动画与物理体必须看到同一帧真值
            _animView?.SetMotion(EngineVelocity, _motor.Facing, EngineVelocity.magnitude);
        }

        private void BeginHitStop(float seconds)
        {
            if (seconds <= 0f || _animView == null || _animView.Animator == null) return;

            _animView.Animator.speed = 0f;
            _hitStopUntil = Time.time + seconds;
        }

        private void RestoreHitStop(float now)
        {
            if (_hitStopUntil <= 0f || now < _hitStopUntil) return;

            RestoreHitStopNow();
        }

        private void RestoreHitStopNow()
        {
            _hitStopUntil = 0f;

            if (_animView != null && _animView.Animator != null) _animView.Animator.speed = 1f;
        }

        public void ApplySlow(float speedScale, float seconds)
        {
            _logic?.Status.ApplySlow(speedScale, seconds);
        }

        /// 冲量拆成大小+方向再递交，零向量会被逻辑层丢掉
        public void ApplyKnockback(Vector2 impulse)
        {
            if (_logic == null) return;

            float magnitude = impulse.magnitude;

            if (magnitude <= 0f) return;

            _logic.ApplyKnockback(magnitude, impulse / magnitude);
        }

        /// 转交逻辑层状态组：麻痹要靠移动门禁才停得住
        public void ApplyStun(float seconds)
        {
            _logic?.Status.ApplyStun(seconds);
        }

        public void ForceLoseAggro()
        {
            _logic?.ForceLoseAggro();
        }

        void IAggroBreakable.ForceLoseAggro() => ForceLoseAggro();

        private void OnDrawGizmosSelected()
        {
            if (_spec == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _spec.Radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _spec.StopDistance);
        }

        private void OnDestroy()
        {
            ReleaseSparkSlot();

            if (_registry != null && _registered) _registry.Unregister(this);
        }

        /// <summary>减速越狠越靠近 enemyBodySlowed；跨 TintStep 才重播</summary>
        private void ApplySlowTint()
        {
            if (Mathf.Abs(_slowMultiplier - _tintScale) < TintStep) return;

            _tintScale = _slowMultiplier;

            if (_slowTintHandle.IsValid) EffectModule.Stop(_slowTintHandle);

            if (_tintScale >= 1f) return;

            EffectContext ctx = EffectContext.OnTarget(transform);
            ctx.Tint = Color.Lerp(ConfigModule.Visuals.enemyBodySlowed, Color.white, Mathf.Clamp01(_tintScale));

            _slowTintHandle = EffectModule.Play(EffectId.SlowTint, in ctx);
        }

        /// <summary>全局最多 SparkCapacity 只同时跳，否则打爆 ChainShock 池</summary>
        private void TickStunSpark(float deltaTime)
        {
            bool stunned = _logic != null && _logic.Status.IsStunned;

            if (!stunned)
            {
                ReleaseSparkSlot();

                _sparkTimer = 0f;

                return;
            }

            if (!_sparkOn)
            {
                if (_sparkActive >= SparkCapacity) return;

                _sparkActive++;
                _sparkOn = true;
            }

            _sparkTimer -= deltaTime;

            if (_sparkTimer > 0f) return;

            _sparkTimer = SparkInterval;

            EffectContext ctx = EffectContext.At(Position);
            ctx.Value = 0.35f;

            EffectModule.Play(EffectId.ChainShock, in ctx);
        }

        private void ReleaseSparkSlot()
        {
            if (!_sparkOn) return;

            _sparkOn = false;
            _sparkActive--;
        }

        private void UpdateCell(bool force)
        {
            if (_registry == null || _grid == null)
            {
                WarnMissingGridOnce();

                return;
            }

            Vector3Int cell = _grid.WorldToCell(Position);

            if (!force && _registered && cell == _currentCell) return;

            _currentCell = cell;

            if (_registered) _registry.Move(this, cell);
            else
            {
                _registry.Register(cell, this);
                _registered = true;
            }

            // 跨格报一次进格：瞬时伤害/击退由格子系统补给，持续效果由格状态自己的节拍提交
            _grid.OnActorEnterCell(cell, this);
        }

        private void WarnMissingGridOnce()
        {
            if (_gridWiringWarned) return;

            _gridWiringWarned = true;

            Debug.LogWarning(
                $"[Enemy] {name} 的格子系统没接线（grid / registry 有一个是 null）：这只敌人踩地块不会减速、不会掉血。" +
                "两条生成路径都该注入它们（CombatDirector / CombatDummyHarness）。",
                this);
        }

        private Vector2 TargetPosition()
        {
            Vector3 p = _target.position;

            return new Vector2(p.x, p.y);
        }

        private void Die(Vector2 hitDirection)
        {
            if (_deathFired) return;

            _deathFired = true;

            // 顿帧中死亡不再 FixedTick，必须显式恢复
            RestoreHitStopNow();

            SfxRequested?.Invoke(AudioId.EnemyDie);

            if (_motor != null) _motor.Move(Vector2.zero);

            // 死亡即刻摘掉归属，否则这一格继续"有目标"且结算方拿到正在销毁的对象
            if (_registry != null && _registered)
            {
                _registry.Unregister(this);
                _registered = false;
            }

            // 先发事实再销毁：回调方要用位置（战利品落在死亡那一刻的地方）
            Died?.Invoke(this);

            if (_collider != null) _collider.enabled = false;

            _animView?.TriggerDie();

            EffectContext ctx = EffectContext.At(Position, hitDirection);
            ctx.Tint = ConfigModule.Visuals.enemyBodyNormal;
            ctx.Value = ConfigModule.VisualFeel.shatterIntensity;

            // 播哪个特效由预制体说了算，本类不写死
            EffectModule.Play(deathEffect, in ctx);

            // 尸体淡出：alpha 源是 Animator 的 FadeAlpha 曲线
            if (_animView != null)
            {
                EffectContext fade = EffectContext.OnTarget(_animView.transform);

                _fadeHandle = EffectModule.Play(EffectId.AlphaTint, in fade);
            }

            // 回收触发口：事件到了立刻收；到点还没来（未挂 AnimationEvent / 无可用 Animator）也收
            bool waitForEvent = _animView != null && _animView.HasValidAnimator;

            if (waitForEvent) _animView.DeathAnimationEnd += OnDeathAnimationDone;

            float fallbackSeconds = ConfigModule.VisualFeel.deathFallbackSeconds;

            if (fallbackSeconds > 0f)
            {
                _respawnRoutine = StartCoroutine(CoFallbackDeath(fallbackSeconds));
            }
            else if (!waitForEvent)
            {
                OnDeathAnimationDone();
            }
        }

        private void OnDeathAnimationDone()
        {
            if (_deathDone) return;

            _deathDone = true;

            if (_animView != null) _animView.DeathAnimationEnd -= OnDeathAnimationDone;

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);

                _respawnRoutine = null;
            }

            if (IsScenePlaced && respawnDelay > 0f)
            {
                _respawnRoutine = StartCoroutine(CoRespawn());

                return;
            }

            Destroy(gameObject);
        }

        private System.Collections.IEnumerator CoFallbackDeath(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            _respawnRoutine = null;   // 这条协程就是 _respawnRoutine，先摘句柄再收口

            OnDeathAnimationDone();
        }

        /// 满血归位：打靶复活与清场复位共用；未接管过的对象（无 spec）不动
        public void ResetToSpawn()
        {
            if (_spec == null) return;

            // 退订并清句柄：否则复活后仍被上条命的淡出压 alpha
            if (_animView != null) _animView.DeathAnimationEnd -= OnDeathAnimationDone;

            if (_fadeHandle.IsValid)
            {
                EffectModule.Stop(_fadeHandle);
                _fadeHandle = EffectHandle.None;
            }

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);

                _respawnRoutine = null;
            }

            transform.position = new Vector3(_spawnPosition.x, _spawnPosition.y, 0f);

            if (_motor != null)
            {
                _motor.SetPosition(_spawnPosition);
                _motor.Move(Vector2.zero);
            }

            Stats = new EnemyStats(_spec);
            _deathFired = false;
            _deathDone = false;

            if (_collider != null) _collider.enabled = true;
            if (_body != null) _body.enabled = true;

            UpdateCell(force: true);
            _animView?.ResetToDefault();

            RestoreHitStopNow();
        }

        private System.Collections.IEnumerator CoRespawn()
        {
            if (_body != null) _body.enabled = false;
            if (_collider != null) _collider.enabled = false;

            yield return new WaitForSeconds(respawnDelay);

            _respawnRoutine = null;

            ResetToSpawn();
        }
    }
}
