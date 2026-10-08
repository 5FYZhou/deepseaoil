using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Projectile
{
    /// <remarks>伪高度：球逻辑上始终走 SampleGround 直线，抛物线只是 SampleVisual 的 y 偏移；落地判定即 t≥1，无需碰撞。时长按距离缩放，距离由调用方用同一夹取函数算好传入，本类型不重算。Start/End 均为贴地逻辑位置，End 是唯一可信落点真值</remarks>
    public readonly struct ProjectileTrajectory
    {
        public readonly BallType Type;

        public readonly Vector2 Start;

        public readonly Vector2 End;

        /// <summary>飞行总时长（秒），已按飞行距离缩放</summary>
        public readonly float Duration;

        public readonly float MaxHeight;

        /// <remarks>离 origin 小于 ProjectileSpec.MinThrowDistance 时被推开到该距离（方向为零时归一化出 NaN）</remarks>
        public ProjectileTrajectory(BallType type, Vector2 origin, Vector2 target, float distance, ProjectileSpec spec)
        {
            float minDistance = spec.MinThrowDistance;

            Vector2 towards = target - origin;
            float actual = towards.magnitude;

            if (actual < minDistance)
            {
                // 距离为0时方向取右，避免除零产生 NaN
                Vector2 direction = actual > 1e-6f ? towards / actual : Vector2.right;

                target = origin + direction * minDistance;
                distance = minDistance;
            }

            Type = type;
            Start = origin;
            End = target;
            MaxHeight = spec.MaxHeight;

            // 按最远距离归一化：最远一投=FlightDuration；Max 防距离≤0 时长变0
            Duration = spec.FlightDuration * (Mathf.Max(distance, minDistance) / spec.MaxThrowDistance);
        }

        /// <summary>t=已飞比例，调用方夹到 0..1</summary>
        public Vector2 SampleGround(float t)
        {
            return Vector2.Lerp(Start, End, t);
        }

        public Vector2 SampleVisual(float t)
        {
            return SampleGround(t) + Vector2.up * (MaxHeight * SampleHeight01(t));
        }

        public static float SampleHeight01(float t)
        {
            return Ballistics.ArcHeight01(t);
        }
    }
}
