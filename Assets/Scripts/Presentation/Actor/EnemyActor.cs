using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Actor
{
    /// <summary>一只敌人：建出刚体与视效、推进逻辑层、结算伤害与死亡；它是这只敌人的组合根。</summary>
    /// <remarks><b>受伤只有一条路</b>：<see cref="TakeDamage"/>；脚底中心每帧登记进 <see cref="EnemyCellRegistry"/>（格子按"人站在哪一格"结算）。
    /// <b>不自己驱动</b>：由 <c>CombatDirector</c> 统一逐只 <see cref="FixedTick"/>，自驱会让帧内顺序不可预测。</remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour, IDamageable, ISlowEffectTarget, IAlivable, IManagedActor
    {
        private const float HpTextCharacterSize = 0.13f;

        /// <summary>头顶数字的垂直偏移；<c>0</c> = 压在圆心（锚点用 <c>MiddleCenter</c>）。</summary>
        private const float HpTextOffsetY = 0f;

        /// <summary>内置字体候选名，按"新版 → 旧版"排；旧名在 2022.3 <b>会抛 <c>ArgumentException</c></b> 而不是返回 <c>null</c>，所以逐个 <c>try</c>。</summary>
        private static readonly string[] BuiltinFontNames = { "LegacyRuntime.ttf", "Arial.ttf" };

        private EnemySpec _spec;
        private EnemyLogic _logic;
        private EnemyMotor _motor;
        private SpriteRenderer _body;
        private TextMesh _hpText;
        private Transform _target;
        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        private bool _registered;
        private Vector3Int _currentCell;

        /// <summary>本帧实际生效的减速乘数（视效读数）；速度那边由门禁经账本落地。<b>一份数据两个消费者</b>：各写一遍会让颜色与速度在某帧不一致，而两者看起来都"没错"。</summary>
        private float _slowMultiplier = 1f;

        public bool IsAlive => Stats != null && Stats.IsAlive;

        /// <inheritdoc />
        /// <remarks>取执行器的物理体位置，不取 <c>transform.position</c>（会被位置积分覆盖）：两个位置真值迟早有一帧对不上，且那一帧没有任何报错。</remarks>
        public Vector2 Position => _motor != null ? _motor.Position : (Vector2)transform.position;

        public EnemyStats Stats { get; private set; }

        public int Hp => Stats != null ? Stats.Hp : 0;

        public bool IsHurt => Stats != null && Stats.IsAlive && _logic != null && _logic.IsHurt;
        public Vector2 EngineVelocity => _motor == null ? Vector2.zero : _motor.EngineVelocity;

        /// <summary>组装一只敌人；依赖全部由参数给出（不留 inspector 字段）。</summary>
        /// <param name="target">追击目标；<c>null</c> 时敌人随即滑停。</param>
        /// <param name="registry"><c>null</c> 时不登记（敌人不会被格子结算）。</param>
        public void Initialize(
            Vector2 position,
            in EnemySpec spec,
            Transform target,
            Vector2 facing,
            GridLogic grid,
            EnemyCellRegistry registry,
            Transform parent)
        {
            _spec = spec;
            _target = target;
            _grid = grid;
            _registry = registry;

            Stats = new EnemyStats(spec);

            transform.position = new Vector3(position.x, position.y, 0f);

            if (parent != null) transform.SetParent(parent, true);

            BuildBody();

            _motor = gameObject.AddComponent<EnemyMotor>();

            // 建完刚体立刻固化物理参数：否则到第一次读位置之间的那个物理步会用默认重力跑。
            _motor.EnsureInitialized();

            _logic = new EnemyLogic(_motor, spec);

            _motor.Facing = facing;

            BuildVisuals();
            UpdateCell(force: true);
        }

        /// <summary>结算一次命中：<b>唯一受伤入口</b>（格子状态转换、近战与陷阱都走这里）。</summary>
        public void TakeDamage(in Damage damage)
        {
            if (Stats == null || !Stats.IsAlive) return;

            if (damage.HasDamage)
            {
                // 耐久是整数，伤害是配置里的浮点数：四舍五入到整数（见 EnemyStats.ApplyDamage）。
                Stats.ApplyDamage(damage.Amount);
            }

            if (damage.HasKnockback && _logic != null)
            {
                // 只递交：冲量到下一次逻辑帧才由状态效果层变成一次"进入受击"；"被撞多远 / 滑多久"归角色配置（hurtDecay）。
                _logic.ApplyKnockback(damage.Impulse, damage.Direction);
            }

            if (!Stats.IsAlive)
            {
                Die(damage.Direction);
                return;
            }

            UpdateHpText();
        }

        /// <summary>推进一个物理帧：算减速、刷视效、上报所在格，然后驱动逻辑层。</summary>
        /// <remarks>由 <c>CombatDirector</c> 调用，<b>不</b>用 <c>Update</c>：速度必须一个物理帧只提交一次。视效同频刷新 —— 颜色与逻辑层用同一份减速系数，分两个频率会有一帧不同步。</remarks>
        public void FixedTick(float now, float deltaTime)
        {
            if (!Stats.IsAlive) return;

            _logic.SetTarget(_target == null ? (Vector2?)null : TargetPosition());
            _logic.Tick(now, deltaTime);

            _slowMultiplier = _logic.Status.SlowScale;

            UpdateCell(force: false);
            UpdateBodyColor();
            UpdateSortingOrder();
        }

        /// <inheritdoc />
        public void ApplySlow(float speedScale, float seconds)
        {
            _logic?.Status.ApplySlow(speedScale, seconds);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _spec.Radius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _spec.StopDistance);
        }

        private void OnDestroy()
        {
            if (_registry != null && _registered) _registry.Unregister(this);
        }

        private void BuildBody()
        {
            gameObject.AddComponent<Rigidbody2D>();

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = _spec.Radius;
        }

        private void BuildVisuals()
        {
            _body = gameObject.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                _body,
                PrimitiveSprites.Circle,
                ConfigModule.Visuals.enemyBodyNormal,
                RenderOrder.ActorOrder(Position.y),
                _spec.Radius * 2f);

            BuildHpText();
            UpdateHpText();
            UpdateBodyColor();
        }

        /// <summary>建敌人圆心的剩余耐久数字；字体取不到时<b>干脆不建数字</b>（<c>TextMesh</c> 没字体会画成方块），而"拿不到"本身也不报错。</summary>
        private void BuildHpText()
        {
            Font font = BuiltinFont();

            if (font == null) return;

            var go = new GameObject("HpText");

            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, HpTextOffsetY, 0f);

            _hpText = go.AddComponent<TextMesh>();

            _hpText.font = font;
            _hpText.fontSize = 64;
            _hpText.characterSize = HpTextCharacterSize;

            // 锚点居中是**配合偏移为 0** 用的：LowerCenter 会让数字从"给定位置"往上长。
            _hpText.anchor = TextAnchor.MiddleCenter;
            _hpText.alignment = TextAlignment.Center;
            _hpText.color = Color.white;

            // 材质必须从字体上取：TextMesh 不会自己找，不给就是粉红方块。
            var textRenderer = _hpText.GetComponent<MeshRenderer>();

            textRenderer.sharedMaterial = font.material;

            // sortingOrder 必须显式设：全部是 z=0 的正交俯视，不设这句数字会和自己的身体抢先后。
            textRenderer.sortingOrder = RenderOrder.ActorOverlay;
        }

        /// <summary>取一个能用的内置字体；一个都拿不到时返回 <c>null</c>（调用方跳过数字，不报错）。</summary>
        private static Font BuiltinFont()
        {
            for (int i = 0; i < BuiltinFontNames.Length; i++)
            {
                try
                {
                    Font font = Resources.GetBuiltinResource<Font>(BuiltinFontNames[i]);

                    if (font != null) return font;
                }
                catch (System.ArgumentException)
                {
                    // 这个版本不认这个名字：静默试下一个。
                }
            }

            return null;
        }

        /// <summary>头顶的剩余耐久数字；<b>耐久为 0 时是空串</b>（显示一个 <c>"0"</c> 会让人以为它还有 0 点血）。</summary>
        public static string HpText(int hp)
        {
            return hp > 0 ? hp.ToString() : string.Empty;
        }

        /// <summary>这一帧该不该亮；闪烁是<b>相位</b>而不是状态（布尔字段会在暂停 / 掉帧时偷偷不同步）。</summary>
        /// <param name="hz">闪烁频率（Hz）；非法值按不闪处理。</param>
        /// <remarks>用 <c>Sin</c> 而不是 <c>(time * hz) % 1</c>：取整在 <c>hz</c> 为 0 时会除零；<c>Sin</c> 在频率 0 时恒为 0（整段受击保持亮色，能看出不对但不崩）。</remarks>
        public static bool IsFlashOn(float time, float hz)
        {
            if (float.IsNaN(hz) || hz <= 0f) return false;

            return Mathf.Sin(time * 2f * Mathf.PI * hz) > 0f;
        }

        private void UpdateHpText()
        {
            if (_hpText == null) return;

            string text = HpText(Hp);

            if (_hpText.text == text) return;

            _hpText.text = text;
        }

        /// <summary>刷新身体颜色：减速变深、受击闪烁，两者可叠加。</summary>
        /// <remarks>闪白相位用 <c>Time.time</c>，不自己累加（累加出来的相位会随帧率漂）；颜色来自观感表 <c>ConfigModule.Visuals</c>，与格子高亮同一个入口。
        /// <c>EffectId.Flash</c> 的驱动尚未实现，所以这里每帧刷一次 color。</remarks>
        private void UpdateBodyColor()
        {
            if (_body == null) return;

            bool flashOn = IsHurt && IsFlashOn(Time.time, _spec.FlashHz);

            _body.color = ConfigModule.Visuals.EnemyBodyColor(_slowMultiplier, flashOn);
        }

        private void UpdateSortingOrder()
        {
            if (_body == null) return;

            _body.sortingOrder = RenderOrder.ActorOrder(Position.y);
        }

        private void UpdateCell(bool force)
        {
            if (_registry == null || _grid == null) return;

            Vector3Int cell = _grid.WorldToCell(Position);

            if (!force && _registered && cell == _currentCell) return;

            _currentCell = cell;

            if (_registered) _registry.Move(this, cell);
            else
            {
                _registry.Register(cell, this);
                _registered = true;
            }
        }

        private Vector2 TargetPosition()
        {
            Vector3 p = _target.position;

            return new Vector2(p.x, p.y);
        }

        private void Die(Vector2 hitDirection)
        {
            if (_motor != null) _motor.Move(Vector2.zero);

            // 死亡即刻摘掉归属：留着它会让这一格继续"有目标"，而结算方拿到的是一个正在销毁的对象。
            if (_registry != null && _registered)
            {
                _registry.Unregister(this);
                _registered = false;
            }

            EffectContext ctx = EffectContext.At(Position, hitDirection);
            ctx.Tint = ConfigModule.Visuals.enemyBodyNormal;

            EffectModule.Play(EffectId.Shatter, in ctx);

            Destroy(gameObject);
        }
    }
}
