using UnityEngine;

namespace DeepseaOil.Data
{
    /// <remarks>只装数值，行为归掉落物实体；SO 不对外暴露</remarks>
    public sealed class DropSpec
    {
        private readonly DropTuning _tuning;

        public DropSpec(DropTuning tuning)
        {
            _tuning = tuning;
        }

        /// <remarks>调色板来源 ConfigModule.Visuals</remarks>
        public Color Color => ConfigModule.Visuals.waterBall;

        /// <summary>抛出到落点时长，秒</summary>
        public float FlightDuration => _tuning.flightDuration;

        /// <summary>弧高，世界单位</summary>
        public float ArcHeight => _tuning.arcHeight;

        /// <summary>落点→玩家速度，单位/秒</summary>
        public float HomingSpeed => _tuning.homingSpeed;

        public float ReachDistance => _tuning.reachDistance;

        public int Amount => _tuning.amount;

        /// <summary>视觉直径，世界单位</summary>
        public float BodyDiameter => _tuning.bodyDiameter;

        public float TriggerRadius => _tuning.triggerRadius;
    }
}
