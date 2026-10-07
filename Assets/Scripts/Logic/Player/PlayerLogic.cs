using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家逻辑：持有<b>账本</b>（血量 ＋ 水球）与<b>三层领域状态机</b>（状态效果 / 战斗 / 移动），
    /// 并回答"我现在能不能做某件事"。
    /// </summary>
    /// <remarks>
    /// <b>它是组合件，不是转发层</b>（审查已定：让 Logic 退化为组合件）：速度账本与控制律在
    /// <see cref="ActorLogic.Motor"/> 上，移动状态在 <see cref="MoveGroup"/> 上，
    /// 生命体征与配置在 <see cref="Stats"/> 上 —— 消费者直接去那几个件取，
    /// 本类不再为它们各开一个同名的转发属性（那种转发只会把"这个值在哪一层"这件事弄糊）。
    /// <para><b>分层与门禁：</b>状态效果层 → 战斗层 → 移动层，每层只向下层输出门禁与锁，
    /// <b>只有移动层拥有写速度的权限</b>。本类是按这个顺序驱动的唯一地方：
    /// <code>
    /// ① StatusGroup.Tick(ctx, 挂起的击退)   // 产出门禁（受击 = 速度被外力接管）
    /// ② （战斗层没有物理帧职责：瞄准与开火都在渲染帧）
    /// ③ MoveGroup.Tick(ctx, 门禁)           // 唯一写速度
    /// </code></para>
    /// <para><b>受击的唯一入口是 <see cref="TakeDamage"/></b>：世界侧只能"通知"，数据仍由玩家侧自己改。</para>
    /// <para><b>击退为什么是"挂起 + 帧内进入受击"：</b>账本在帧首就把累积区清了，而世界侧的结算
    /// 跑在玩家那一帧<b>之后</b> —— 帧外直接累加的冲量一定会被下一帧帧首清掉
    /// （旧实现正是这么写的，现象是"被撞了但纹丝不动"，而且不报错）。</para>
    /// </remarks>
    public sealed class PlayerLogic : ActorLogic
    {
        private readonly PlayerStats _stats;
        private readonly InputBuffer _buffer;
        private readonly StatusGroup _status;
        private readonly CombatGroup _combat;
        private readonly MoveGroup _moveGroup;

        /// <summary>已递交、还没被状态效果层消费的击退冲量（可在帧外累加）。</summary>
        private Vector2 _pendingKnockback;

        /// <param name="motor">移动执行器（账本 ＋ 控制律 ＋ 物理体读写）。</param>
        /// <param name="spec">玩家取值边界（表行 ＋ 移动 SO ＋ 投掷调参）。</param>
        /// <param name="buffer">按键沿缓冲（冲刺窗口）。</param>
        public PlayerLogic(IActorMotor motor, PlayerSpec spec, InputBuffer buffer)
            : base(motor, spec.Config)
        {
            _stats = new PlayerStats(spec);
            _buffer = buffer;

            _status = new StatusGroup(this);
            _combat = new CombatGroup(this);
            _moveGroup = new MoveGroup(this, spec, motor, buffer);
        }

        /// <summary>账本：血量 ＋ 水球。<b>世界侧经它拿读数、经本类入口改数据</b>。</summary>
        public PlayerStats Stats => _stats;

        /// <summary>移动层（走 / 站 / 冲刺）。<b>当前移动状态去这里取</b>（<c>MoveGroup.Current</c>）。</summary>
        public MoveGroup MoveGroup => _moveGroup;

        /// <summary>状态效果层（受击 / 减速修饰）。</summary>
        public StatusGroup Status => _status;

        /// <summary>战斗层（瞄准 / 投掷资格）。</summary>
        public CombatGroup Combat => _combat;

        /// <summary>是否还有血。</summary>
        public bool IsAlive => _stats.IsAlive;

        // ─────────────────────────────────────────────
        // 装配期注入（世界侧组合根调一次）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 注入瞄准所需的世界信息与投掷裁决口。
        /// </summary>
        /// <param name="geometry">格子几何。</param>
        /// <param name="maxThrowDistance">射程上限（见 <c>CombatGroup.Configure</c> 的数值口径说明）。</param>
        /// <param name="sink">世界侧的裁决口。</param>
        public void ConfigureAim(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _combat.Configure(in geometry, maxThrowDistance, sink);
        }

        // ─────────────────────────────────────────────
        // 渲染帧（由 PlayerController.RenderTick 驱动）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 渲染帧：算一次瞄准并去重发布事实。
        /// </summary>
        /// <param name="aimWorld">瞄准点（世界坐标）。"屏幕 → 世界"的换算在表现层，只有它认识相机。</param>
        /// <param name="now">渲染帧时间（<c>Time.time</c>）。</param>
        public void UpdateAim(Vector2 aimWorld, float now)
        {
            _combat.UpdateAim(Motor.Position, aimWorld, now);
        }

        /// <summary>收起瞄准（暂停 / 拿不到相机时）。</summary>
        public void ClearAim()
        {
            _combat.ClearAim();
        }

        /// <summary>
        /// 表达一次投掷意图（"我想把这一颗球扔到瞄准的那一格"）。
        /// </summary>
        /// <param name="ball">球种。</param>
        /// <param name="now">渲染帧时间。</param>
        /// <returns>被世界侧采纳为 <c>true</c>。</returns>
        public bool RequestThrow(BallType ball, float now)
        {
            return _combat.RequestThrow(ball, now);
        }

        // ─────────────────────────────────────────────
        // 物理帧
        // ─────────────────────────────────────────────

        /// <summary>
        /// 结算一次伤害。<b>世界侧唯一的受伤入口</b>（接触、陷阱、将来的技能都走这里）。
        /// </summary>
        /// <param name="damage">命中事实（伤害值 ＋ 可选的击退）。</param>
        /// <param name="now">当前时间（由驱动方给出；本类不读 <c>UnityEngine.Time</c>）。</param>
        /// <returns>真的生效了为 <c>true</c>（被无敌帧挡掉时为 <c>false</c>）。</returns>
        /// <remarks>
        /// <b>被挡掉时三件事一起不发生</b>：不扣血、不推、也不写无敌时间。
        /// <para>冲量只是<b>挂起</b>，真正的接管发生在下一次 <see cref="OnTick"/>：
        /// 状态效果层把它变成一次"进入受击"，再由移动层落实到速度上。</para>
        /// </remarks>
        public bool TakeDamage(in Damage damage, float now)
        {
            if (!damage.HasDamage && !damage.HasKnockback) return false;

            if (damage.HasDamage && !_stats.ApplyDamage(damage.Amount, now)) return false;

            if (damage.HasKnockback)
            {
                Vector2 impulse = damage.Direction * damage.Impulse;
                float limit = _stats.Spec.KnockbackSpeedLimit;

                // 上限非法（0 / 非数）时按"不限"处理：ClampMagnitude 遇到 0 会把冲量整个吃掉
                _pendingKnockback = limit > 0f && !float.IsNaN(limit)
                    ? Vector2.ClampMagnitude(impulse, limit)
                    : impulse;
            }

            return true;
        }

        /// <summary>
        /// 把角色瞬移到给定位置（重生 / 传送用），并把血量恢复满、停住、清掉挂起的击退。
        /// </summary>
        /// <remarks>
        /// 走执行器的物理体位置而不是 <c>transform.position</c>：后者会被刚体的位置积分覆盖掉，
        /// 表现为"瞬移了一下又弹回去"。它<b>不</b>是"移动"：不经过状态机、不改朝向、不产生提交。
        /// <para><b>速度要硬清零</b>（<c>Motor.Move(zero)</c>）：账本里的 <c>StopMove</c> 只影响
        /// "本帧该提交什么"，而重生是瞬移 —— 物理体上残留着上一局的速度时，
        /// 复活后会自己滑一段。这不是"第二个速度写者"：本类就是那个写者，
        /// 这里只是它自己的一次硬清零。</para>
        /// </remarks>
        public void RespawnTo(Vector2 position)
        {
            _stats.ResetToFull();

            Motor.StopMove();                  // 账本层：本帧不再提交任何速度

            _pendingKnockback = Vector2.zero;

            Motor.Move(Vector2.zero);          // 引擎层：把残留速度清掉（否则复活后会继续滑）
            Motor.SetPosition(position);
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // ① 状态效果层：帧外挂起的击退在这里变成一次"进入受击"，并产出本帧门禁
            _status.Tick(in ctx, _pendingKnockback);
            _pendingKnockback = Vector2.zero;

            // ② 战斗层：物理帧没有职责（瞄准与开火都在渲染帧），故不参与本序列

            // ③ 移动层：唯一写速度的地方；上层门禁在这里落地
            _moveGroup.Tick(in ctx, _status.Gates);
        }
    }
}
