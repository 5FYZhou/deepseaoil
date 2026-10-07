using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>一种掉落物的取值边界：持有 <c>DropTuning</c> SO，暴露被消费的语义点。</summary>
    /// <remarks>本类只装数值；行为（要不要追踪玩家 / 会不会被吸走 / 落地有没有音效）归掉落物实体那一侧。SO 不对外暴露。</remarks>
    public sealed class DropSpec
    {
        private readonly DropTuning _tuning;

        public DropSpec(DropTuning tuning)
        {
            _tuning = tuning;
        }

        /// <remarks>调色板唯一权威来源是 <c>ConfigModule.Visuals</c>，本属性只是按掉落物的语义解析一次。</remarks>
        public Color Color => ConfigModule.Visuals.waterBall;

        /// <summary>从生成点抛到落点的时长（秒）。</summary>
        public float FlightDuration => _tuning.flightDuration;

        /// <summary>抛物线的弧高（世界单位）。</summary>
        public float ArcHeight => _tuning.arcHeight;

        /// <summary>落点 → 玩家的飞行速度（单位/秒）。</summary>
        public float HomingSpeed => _tuning.homingSpeed;

        /// <summary>判定"够到玩家了"的距离（世界单位）。</summary>
        public float ReachDistance => _tuning.reachDistance;

        /// <summary>领取一次给几个（载荷数量，不写死在订阅方）。</summary>
        public int Amount => _tuning.amount;

        /// <summary>本体视觉直径（世界单位）。</summary>
        public float BodyDiameter => _tuning.bodyDiameter;

        /// <summary>触发半径（世界单位）。</summary>
        public float TriggerRadius => _tuning.triggerRadius;
    }
}
