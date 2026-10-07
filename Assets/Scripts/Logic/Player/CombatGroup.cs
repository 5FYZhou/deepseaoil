using DeepseaOil.Foundation;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>玩家战斗层：瞄准 ＋ 投掷资格（弹药 / 冷却）＋ 递交投掷意图。</summary>
    /// <remarks>
    /// 帧相位：瞄准与开火是非物理逻辑，由 <c>PlayerController.RenderTick</c> 每渲染帧驱动一次。
    /// 本类时钟是 <c>Time.time</c>，移动层是 <c>Time.fixedTime</c> —— 混用会让"冷却看起来有时长、有时短"。
    /// </remarks>
    public sealed class CombatGroup
    {
        private readonly PlayerLogic _logic;

        private readonly Cooldown _throwCooldown = new Cooldown();

        /// <summary>世界侧注入的裁决口；未注入时"投不出去"（静默拒绝）。</summary>
        private IThrowSink _sink;

        private GridGeometry _geometry;
        private float _maxThrowDistance;

        private Vector2 _origin;

        private bool _hasPublished;
        private AimChanged _published;

        public CombatGroup(PlayerLogic logic)
        {
            _logic = logic;
        }

        public bool HasAim { get; private set; }

        /// <summary>当前瞄准格；<see cref="HasAim"/> 为 <c>false</c> 时无意义。</summary>
        public Vector3Int AimCell { get; private set; }

        /// <summary>玩家侧是否可投（射程内 ＋ 冷却就绪 ＋ 有水球）。</summary>
        public bool AimAvailable { get; private set; }

        /// <summary>装配期注入（由世界侧组合根调一次）：格子几何、投掷射程上限（住在 <c>projectile</c> 表，读取方是玩家侧资格）、裁决口。</summary>
        public void Configure(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _geometry = geometry;
            _maxThrowDistance = maxThrowDistance;
            _sink = sink;
        }

        /// <summary>渲染帧：算一次瞄准，去重发布 <see cref="AimChanged"/>。</summary>
        public void UpdateAim(Vector2 origin, Vector2 aimWorld, float now)
        {
            _origin = origin;

            GridGeometry geometry = _geometry;
            Vector3Int cell = default;

            bool hasAim = geometry.IsValid
                && TileAim.TryGetAimCell(in geometry, origin, aimWorld, _maxThrowDistance, out cell);

            HasAim = hasAim;
            AimCell = hasAim ? cell : default;

            // 玩家的高亮口径：射程内（上面已保证）＋ 冷却就绪 ＋ 有水球；土球是副攻击、不吃弹药，不参与这一条。
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

        /// <summary>表达一次投掷意图：资格在本层，采纳与否由世界侧裁决。</summary>
        /// <remarks>被拒绝时什么都不发生：不扣弹药、不进冷却。否则"朝墙上点一下"会白白吃掉半秒。</remarks>
        public bool RequestThrow(BallType ball, float now)
        {
            if (!HasAim) return false;
            if (!_throwCooldown.CanUse(now)) return false;
            if (ball == BallType.Water && _logic.Stats.WaterBallCount <= 0) return false;
            if (_sink == null) return false;

            var intent = new ThrowIntent(ball, AimCell, _origin, _geometry.CellCenter(AimCell));

            if (!_sink.RequestThrow(in intent)) return false;

            if (ball == BallType.Water) _logic.Stats.TryConsumeWater(1);

            _throwCooldown.MarkUsed(now, _logic.Stats.Spec.AttackInterval);

            return true;
        }

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
