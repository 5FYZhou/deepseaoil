using DeepseaOil.Foundation;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Logic.Player
{
    /// <summary>战斗层：瞄准+投掷资格（弹药/冷却）+投掷意图；PlayerController.RenderTick 每渲染帧驱动；时钟 Time.time，与移动层 Time.fixedTime 混用会让冷却忽长忽短</summary>
    public sealed class CombatGroup
    {
        private readonly PlayerLogic _logic;

        private readonly Cooldown _throwCooldown = new Cooldown();

        /// <summary>裁决口，未注入即静默拒绝</summary>
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

        public Vector3Int AimCell { get; private set; }

        public bool AimAvailable { get; private set; }

        /// <summary>装配期注入一次：几何、射程上限（projectile 表）、裁决口</summary>
        public void Configure(in GridGeometry geometry, float maxThrowDistance, IThrowSink sink)
        {
            _geometry = geometry;
            _maxThrowDistance = maxThrowDistance;
            _sink = sink;
        }

        /// <summary>渲染帧算瞄准，去重发布</summary>
        public void UpdateAim(Vector2 origin, Vector2 aimWorld, float now)
        {
            _origin = origin;

            GridGeometry geometry = _geometry;
            Vector3Int cell = default;

            bool hasAim = geometry.IsValid
                && TileAim.TryGetAimCell(in geometry, origin, aimWorld, _maxThrowDistance, out cell);

            HasAim = hasAim;
            AimCell = hasAim ? cell : default;

            // 高亮口径：射程内+冷却就绪+有水球；土球是副攻击不吃弹药
            AimAvailable = hasAim && _throwCooldown.CanUse(now) && _logic.Stats.WaterBallCount > 0;

            PublishIfChanged();
        }

        public void ClearAim()
        {
            HasAim = false;
            AimCell = default;
            AimAvailable = false;

            PublishIfChanged();
        }

        /// <summary>投掷意图，采纳由世界侧裁决</summary>
        /// <remarks>被拒绝时不扣弹药不进冷却</remarks>
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
