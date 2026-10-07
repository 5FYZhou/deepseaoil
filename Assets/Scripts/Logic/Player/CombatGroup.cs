using DeepseaOil.Foundation;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>
    /// 玩家<b>战斗层</b>：瞄准 ＋ 投掷资格（弹药 / 冷却）＋ 递交投掷意图。
    /// </summary>
    /// <remarks>
    /// <b>它当前没有状态机，这是判据的结论而不是省事：</b>审查给的判据是
    /// "冷却期间有没有行为" —— 有行为（后摇）才该做成状态。现在投掷在冷却期<b>没有行为</b>
    /// （人能照常走、照常瞄），所以它只是"资格计时器"，收成一个字段（<c>Cooldown</c>）。
    /// 将来出现投掷后摇（不能转向 / 播动画）时，这里才升级成状态组 —— 那也是它叫 Group 的原因。
    /// <para><b>帧相位</b>：瞄准与开火都是<b>非物理逻辑</b>，由 <c>PlayerController.RenderTick</c>
    /// 每渲染帧驱动一次（物理帧只放"角色移动 ＋ 参与物理的逻辑"）。因此本类的时钟是
    /// <c>Time.time</c>，与移动层的 <c>Time.fixedTime</c> 是两个相位各自的时钟 —— 这是刻意的：
    /// 混用会让"冷却看起来有时长、有时短"。</para>
    /// <para><b>它不写弹药、不生成球</b>：弹药是账本的事（<c>PlayerStats</c>），生成球是世界侧的事
    /// （经 <see cref="IThrowSink"/>）。本类只回答"我能不能表达这个意图"，然后表达它。</para>
    /// </remarks>
    public sealed class CombatGroup
    {
        private readonly PlayerLogic _logic;

        /// <summary>投掷冷却（战斗层的资格计时器）。</summary>
        private readonly Cooldown _throwCooldown = new Cooldown();

        /// <summary>世界侧注入的裁决口；未注入时"投不出去"（静默拒绝）。</summary>
        private IThrowSink _sink;

        private GridGeometry _geometry;
        private float _maxThrowDistance;

        /// <summary>本帧瞄准时的出手点（同一帧里的两份位置会漂，所以记一份）。</summary>
        private Vector2 _origin;

        private bool _hasPublished;
        private AimChanged _published;

        public CombatGroup(PlayerLogic logic)
        {
            _logic = logic;
        }

        /// <summary>是否瞄到了可用的格。</summary>
        public bool HasAim { get; private set; }

        /// <summary>当前瞄准格；<see cref="HasAim"/> 为 <c>false</c> 时无意义。</summary>
        public Vector3Int AimCell { get; private set; }

        /// <summary>玩家侧是否可投（射程内 ＋ 冷却就绪 ＋ 有水球）。</summary>
        public bool AimAvailable { get; private set; }

        /// <summary>
        /// 装配期注入（由世界侧组合根调一次）：格子几何、投掷射程上限、裁决口。
        /// </summary>
        /// <param name="geometry">格子几何（"瞄准吸附"要的世界 ↔ 格换算）。</param>
        /// <param name="maxThrowDistance">投掷射程上限。
        /// <b>数值口径</b>：它现在住在 <c>projectile</c> 表里，但读取方是<b>玩家侧资格</b>
        /// （"我能不能表达这个意图"）；表列将来若迁到 <c>player</c> 表，改的只是注入处。</param>
        /// <param name="sink">裁决口；<c>null</c> 表示这个世界没有投掷能力（意图一律被拒）。</param>
        public void Configure(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _geometry = geometry;
            _maxThrowDistance = maxThrowDistance;
            _sink = sink;
        }

        /// <summary>
        /// 渲染帧：算一次瞄准，去重发布 <see cref="AimChanged"/>。
        /// </summary>
        /// <param name="origin">出手点（玩家位置）。</param>
        /// <param name="aimWorld">瞄准点（鼠标世界坐标；屏幕 → 世界的换算在表现层）。</param>
        /// <param name="now">渲染帧时间。</param>
        public void UpdateAim(Vector2 origin, Vector2 aimWorld, float now)
        {
            _origin = origin;

            // 局部变量先落地：`out` 与 `in` 不能同时写在同一次调用里（几何是属性时也是同理）
            GridGeometry geometry = _geometry;
            Vector3Int cell = default;

            bool hasAim = geometry.IsValid
                && TileAim.TryGetAimCell(in geometry, origin, aimWorld, _maxThrowDistance, out cell);

            HasAim = hasAim;
            AimCell = hasAim ? cell : default;

            // 玩家的高亮口径：射程内（上面已保证）＋ 冷却就绪 ＋ 有水球。
            // 土球是副攻击、不吃弹药，所以它不参与这一条 —— 与旧表现（红白只表示有没有弹药）同源，
            // 只是补上了"冷却没到也不该亮"这个缺口。
            AimAvailable = hasAim && _throwCooldown.CanUse(now) && _logic.Stats.WaterBallCount > 0;

            PublishIfChanged();
        }

        /// <summary>收起瞄准（暂停 / 没有相机 / 没有鼠标时由表现层调）。</summary>
        public void ClearAim()
        {
            HasAim = false;
            AimCell = default;
            AimAvailable = false;

            PublishIfChanged();
        }

        /// <summary>
        /// 表达一次投掷意图：<b>资格在本层，采纳与否由世界侧裁决</b>。
        /// </summary>
        /// <param name="ball">球种。</param>
        /// <param name="now">渲染帧时间。</param>
        /// <returns>被采纳（球已经飞出去）为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>被拒绝时什么都不发生</b>：不扣弹药、不进冷却 —— 否则"朝墙上点一下"就会白白吃掉半秒。
        /// 这也是"玩家只表达意图"这条边界的完整含义。
        /// </remarks>
        public bool RequestThrow(BallType ball, float now)
        {
            if (!HasAim) return false;
            if (!_throwCooldown.CanUse(now)) return false;
            if (ball == BallType.Water && _logic.Stats.WaterBallCount <= 0) return false;
            if (_sink == null) return false;

            var intent = new ThrowIntent(ball, AimCell, _origin, _geometry.CellCenter(AimCell));

            if (!_sink.RequestThrow(in intent)) return false;

            // 采纳之后才扣弹药、才进冷却
            if (ball == BallType.Water) _logic.Stats.TryConsumeWater(1);

            _throwCooldown.MarkUsed(now, _logic.Stats.Spec.AttackInterval);

            return true;
        }

        /// <summary>去重发布：瞄准是每帧算的，而"变了"才是事实。</summary>
        private void PublishIfChanged()
        {
            if (_hasPublished
                && _published.HasAim == HasAim
                && _published.Available == AimAvailable
                && (!HasAim || _published.Cell == AimCell))
            {
                return;
            }

            _hasPublished = true;
            _published = new AimChanged(HasAim, AimCell, AimAvailable);

            EventBus<AimChanged>.Publish(_published);
        }
    }
}
