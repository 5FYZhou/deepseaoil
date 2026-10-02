using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 玩家血量：接触伤害、无敌帧、被打退、打空重来。<b>组合根，由 <c>ThrowSpawner</c> 建出。</b>
    /// </summary>
    /// <remarks>
    /// <b>为什么血量写在白模里而不是 <c>PlayerLogic</c>：</b>逻辑层的硬约束是"零引擎类型"，
    /// 而这里每一条都离不开引擎事实（接触检测、<c>Time.fixedTime</c>、刚体位置）。
    /// 逻辑层只被喂进一件事：一次冲量（<c>PlayerLogic.AddImpulse</c>）。
    /// <para><b>本类不写刚体速度。</b>击退走 <see cref="PlayerController.OverrideLogicSpeedTargets"/> ＋
    /// <c>PlayerLogic.AddImpulse</c>，于是"玩家速度只有一个写者"这条不变量在白模里也没被打破。
    /// 这一条正是文档里记的那笔技术债 —— 落地冲量那次绕过了账本，这次不绕。</para>
    /// <para><b>接触伤害靠无敌帧挡，不靠"离开再回来"。</b>玩家贴在敌人身上时每一帧都会被查到，
    /// 没有无敌帧就是每帧扣 10 点、十帧打空 —— 那不是"被打了十下"，是一瞬间死。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        private PlayerController _controller;
        private PlayerLogic _logic;
        private Transform _visual;
        private Vector2 _spawnPoint;
        private float _hp;
        private float _invulnerableUntil = float.NegativeInfinity;
        private float _retryAt = float.NegativeInfinity;
        private bool _paused;
        private EnemyDirector _director;

        /// <summary>玩家碰撞盒的半宽（世界单位）。</summary>
        /// <remarks>
        /// 只用于把血量色块从玩家的中心挪到"脚下"，不参与任何判定 ——
        /// 判定用的是 <see cref="ThrowConstants.ENEMY_CONTACT_RADIUS"/>。
        /// 取 0.5 只是因为场景里的玩家精灵是 1 米见方。
        /// </remarks>
        private const float PlayerHalfWidth = 0.5f;

        /// <summary>供接触检测复用的缓冲，避免每帧分配。</summary>
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        /// <summary>当前血量。</summary>
        public float Current => _hp;

        /// <summary>血上限。</summary>
        public float Maximum => ThrowConstants.PLAYER_MAX_HP;

        /// <summary>是否处于无敌期。</summary>
        public bool IsInvulnerable => !CanTakeDamage(Time.fixedTime, _invulnerableUntil);

        /// <summary>
        /// 无敌帧判据。<b>静态纯函数</b>，因此 EditMode 测试能直接喂时间戳。
        /// </summary>
        /// <param name="now">当前时间。</param>
        /// <param name="invulnerableUntil">无敌结束时间；从未受击时可以是 <c>NaN</c> 或负无穷。</param>
        /// <returns>可以承受伤害为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>写成 <c>!(now &lt; invulnerableUntil)</c> 而不是 <c>now &gt;= invulnerableUntil</c>，</b>
        /// 是为了让非法时间戳落到"可以受伤"这一侧：两个操作数里有 <c>NaN</c> 时前者为 <c>true</c>（照常结算），
        /// 后者为 <c>false</c>（玩家变成<b>永久无敌</b>，而屏幕上什么都不会显示）。
        /// 白模里"看起来在受伤却永远不死"比"挨了一下不该挨的打"糟得多 —— 前者查不出来。
        /// </remarks>
        public static bool CanTakeDamage(float now, float invulnerableUntil)
        {
            return !(now < invulnerableUntil);
        }

        /// <summary>
        /// 组装。
        /// </summary>
        /// <param name="controller">玩家组合根（血量与击退都要它的逻辑层入口）。</param>
        /// <param name="spawnPoint">出生点：打空之后回到这里。</param>
        /// <param name="director">敌人调度器；打空时清场。可以为 <c>null</c>。</param>
        public void Initialize(PlayerController controller, Vector2 spawnPoint, EnemyDirector director)
        {
            if (controller == null || controller.Logic == null)
            {
                Debug.LogError("PlayerHealth 没有拿到玩家的逻辑层，血量不会生效，已停用。", this);
                enabled = false;
                return;
            }

            _controller = controller;
            _logic = controller.Logic;
            _director = director;
            _spawnPoint = spawnPoint;
            _hp = ThrowConstants.PLAYER_MAX_HP;

            BuildVisual();

            EventBus<GamePaused>.Subscribe(OnPaused);
            EventBus<GameResumed>.Subscribe(OnResumed);
        }

        private void OnDestroy()
        {
            EventBus<GamePaused>.Unsubscribe(OnPaused);
            EventBus<GameResumed>.Unsubscribe(OnResumed);
        }

        private void OnPaused(GamePaused evt)
        {
            _paused = true;
        }

        private void OnResumed(GameResumed evt)
        {
            _paused = false;
        }

        private void FixedUpdate()
        {
            if (_paused || _logic == null) return;

            float now = Time.fixedTime;

            // 已经打空：等重试延时，然后回到出生点重来。
            if (_hp <= 0f)
            {
                if (now >= _retryAt) ResetToSpawn();

                return;
            }

            if (TouchEnemy()) ApplyDamage(ThrowConstants.PLAYER_CONTACT_DAMAGE, now);
        }

        /// <summary>
        /// 把血量色块跟到玩家脚下。
        /// </summary>
        /// <remarks>
        /// 用 <c>Update</c> 而不是 <c>FixedUpdate</c>：它只是视觉，没有逻辑后果（玩家位置读自
        /// <c>transform</c>，而刚体已经把它写好了）。放在 <c>FixedUpdate</c> 里反而会让"每物理帧摆一次"
        /// 与"每渲染帧看一次"错拍，高速移动时色块看起来会拖。
        /// </remarks>
        private void Update()
        {
            if (_visual == null) return;

            Vector3 p = transform.position;

            _visual.transform.position = new Vector3(p.x, p.y - PlayerHalfWidth, 0f);
        }

        /// <summary>
        /// 扣血。<b>唯一入口</b>（接触、将来的陷阱与技能都走这里）。
        /// </summary>
        /// <param name="amount">伤害值；非正数直接忽略。</param>
        /// <param name="now">当前时间。</param>
        /// <returns>真的扣掉了血为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>被挡掉时不扣血、不推、不写无敌时间</b>：三件事必须一起发生或一起不发生。
        /// 只扣血不推，玩家会被粘在敌人身上连扣；只推不写无敌，下一帧立刻再扣一次。
        /// <para>次数上限由 <see cref="ThrowConstants.PLAYER_INVULNERABLE_DURATION"/> 决定，
        /// 所以"一次接触只算一下"是**唯一**成立的行为 —— 这也是白模能验的那条。</para>
        /// </remarks>
        public bool ApplyDamage(float amount, float now)
        {
            if (amount <= 0f) return false;
            if (!CanTakeDamage(now, _invulnerableUntil)) return false;

            _hp = Mathf.Max(0f, _hp - amount);

            _invulnerableUntil = now + ThrowConstants.PLAYER_INVULNERABLE_DURATION;

            Knockback();

            if (_hp <= 0f)
            {
                _retryAt = now + ThrowConstants.PLAYER_RETRY_DELAY;

                Debug.Log($"[白模] 玩家被打空（{ThrowConstants.PLAYER_MAX_HP:F0} 点血），{ThrowConstants.PLAYER_RETRY_DELAY:F1}s 后重来");
            }
            else
            {
                Debug.Log($"[白模] 玩家受损：剩余 {_hp:F0}/{ThrowConstants.PLAYER_MAX_HP:F0}");
            }

            return true;
        }

        /// <summary>
        /// 回到出生点并满血。
        /// </summary>
        /// <remarks>
        /// 白模不做失败结算与开局 UI：需求书里的"备战 / 战斗倒计时"还没到做的时候，
        /// 而一个能反复试的白模比一个"死了就停住"的白模有用得多。
        /// <para>清场是必须的：不清的话复活瞬间就贴着原来的敌人，下一次接触在 0.8 秒后又发生一次。</para>
        /// </remarks>
        private void ResetToSpawn()
        {
            _hp = ThrowConstants.PLAYER_MAX_HP;

            // 先把无敌清掉：清场之后玩家已经不在敌人身边，不需要靠无敌撑过重开的那一帧。
            _invulnerableUntil = float.NegativeInfinity;

            _logic.StopMove();

            Vector2 position = _spawnPoint;

            // 边界钳位：出生点理论上在图内，但白模的出生点来自运行期读到的玩家位置，
            // 而那张地图的边界在 TestThrow 里是 40×25 的 MapBounds —— 越界时钳回来，
            // 否则玩家会回到一张"看不见自己"的地图外。
            if (_controller.World.Bounds.TryClamp(position, out Vector2 clamped)) position = clamped;

            _logic.ResetTo(position);

            Debug.Log($"[白模] 玩家重置：位置 ({position.x:F2}, {position.y:F2})，满血");
        }

        /// <summary>
        /// 本帧是否有敌人贴在玩家身上。
        /// </summary>
        /// <remarks>
        /// <b>判定用圆心距，不用接触点与法线。</b>玩家（0.7×0.5 的方块）与敌人（半径 0.45 的圆）
        /// 都只有一个碰撞体，圆心距的结论与逐点接触完全一致，
        /// 而圆心距能用 EditMode 测试直接喂坐标 —— 接触点与法线不能。
        /// <para>接触半径是"标签"而不是 <c>collider.Distance</c>：后者会随两个碰撞体的实际穿透量浮动，
        /// 于是"贴住了"的判定会随物理求解精度变化，不能测。</para>
        /// <para><b>与 <c>PlayerController.FixedUpdate</c> 里那个 <c>World.Bounds</c> 是两件事，
        /// 别被同名绕进去：</b>那个是地图可行走矩形（出生点钳位用），这是接触判定半径。</para>
        /// </remarks>
        private bool TouchEnemy()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, ThrowConstants.ENEMY_CONTACT_RADIUS, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                EnemyActor enemy = _overlapBuffer[i].GetComponentInParent<EnemyActor>();

                if (enemy == null || enemy.IsDead) continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 把玩家推开一次。
        /// </summary>
        /// <remarks>
        /// <b>顺序不能反：先声明本帧上限、再累加冲量。</b>
        /// 上限压的是帧末那一次写出，冲量是往账本里加量 —— 反过来的话冲量这一帧不会被压住，
        /// 连着挨打就会越推越快（而"越推越快"在白模里看起来只是"手感飘"，不会报错）。
        /// </remarks>
        private void Knockback()
        {
            Vector2 away = AwayFromNearestEnemy();

            _controller.OverrideLogicSpeedTargets(_logic, ThrowConstants.PLAYER_KNOCKBACK_SPEED_LIMIT);
            _logic.AddImpulse(away * ThrowConstants.PLAYER_KNOCKBACK_IMPULSE);
        }

        /// <summary>从最近的敌人指向玩家的单位方向；没有敌人时给"下"。</summary>
        private Vector2 AwayFromNearestEnemy()
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, ThrowConstants.ENEMY_CONTACT_RADIUS, _overlapBuffer);

            var self = new Vector2(transform.position.x, transform.position.y);
            Vector2 best = Vector2.down;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                EnemyActor enemy = _overlapBuffer[i].GetComponentInParent<EnemyActor>();

                if (enemy == null || enemy.IsDead) continue;

                Vector3 p = enemy.transform.position;
                var position = new Vector2(p.x, p.y);

                Vector2 delta = self - position;
                float distance = delta.sqrMagnitude;

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.down;
            }

            return best;
        }

        private void BuildVisual()
        {
            var go = new GameObject("玩家血量色块");

            go.transform.SetParent(transform, true);

            // 摆在"脚下"而不是正中：正中会盖住场景里那个玩家精灵，
            // 而白模要看清楚玩家还站在那里（血量是叠加读数，不是替换）。
            Vector3 p = transform.position;
            go.transform.position = new Vector3(p.x, p.y - PlayerHalfWidth, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                renderer,
                PrimitiveSprites.Circle,
                ThrowConstants.PLAYER_BODY_COLOR,
                ThrowConstants.PLAYER_BODY_SORTING_ORDER,
                ThrowConstants.PLAYER_BODY_RADIUS_METERS * 2f
                );

            // 让 Update 每帧把色块摆到玩家脚下（玩家会跑，只摆一次会在几秒后变成"地上有个黄点"）。
            _visual = go.transform;
        }

        private void OnGUI()
        {
            // 白模用的最小读数。**不加进度条、不加倒计时**——
            // 需求书里的备战 15 秒 / 战斗 45 秒还没到做的时候，提前加会让这份临时界面变成"框架"。
            //
            // 敌人那一栏读的是**存活数**（不是"列表里有几个"，那个会把待销毁的也算上），
            // 并且带出最近一只的距离与速度 —— 那两项是"敌人在动吗"唯一可靠的读法。
            int enemies = _director != null ? _director.ActiveCount : FindObjectsOfType<EnemyActor>().Length;
            int mud = FindObjectsOfType<MudPatch>().Length;

            float invulnerable = Mathf.Max(0f, _invulnerableUntil - Time.fixedTime);

            string enemyText = _director == null
                ? $"敌人 {enemies}"
                : $"敌人 {enemies}  最近 {_director.NearestEnemyDistance:F2}m @ {_director.NearestEnemyVelocity.magnitude:F2}";

            string text = _hp <= 0f
                ? $"血量 0/{Maximum:F0}  {enemyText}  泥浆 {mud}  重来 {Mathf.Max(0f, _retryAt - Time.fixedTime):F1}s"
                : $"血量 {_hp:F0}/{Maximum:F0}  {enemyText}  泥浆 {mud}  无敌 {invulnerable:F2}s";

            // **锚在右上角，不是左上角。** 左上是 MovementDebugPanel 的地盘
            // （panelOrigin (8,8)、panelSize 440×190），压上去就读不出来了。
            // 从屏幕右边往左撑开，所以窗口变窄时它自己会往左让，而不是被切掉。
            float width = ThrowConstants.PLAYER_HUD_WIDTH;

            GUI.Box(new Rect(Screen.width - width - 8f, 8f, width, 26f), text);
        }
    }
}
