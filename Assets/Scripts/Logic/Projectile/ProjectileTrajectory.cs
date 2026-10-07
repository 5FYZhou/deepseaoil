using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Foundation;

namespace DeepseaOil.Logic.Projectile
{
    /// <remarks><b>伪高度：</b>世界坐标里球始终在 <see cref="SampleGround"/> 那条<b>直线</b>上（逻辑位置，阴影走这条线），抛物线只体现在 <see cref="SampleVisual"/> 多出来的那个 y 偏移上（视觉位置，球体走这条线）；"落地判定"因此是一次纯数学的 <c>t ≥ 1</c>，不需要碰撞检测。
    /// <b>时长按距离缩放</b>（无论投多远都固定时长的话，贴脸投的球会以极慢的速度飘出去）；距离由调用方用<b>同一个</b>夹取函数算好后传进来，本类型不自己再算一遍 —— 两处各夹一次必然漂移，表现是"指示器画在这里、球落在那里"。
    /// <see cref="Start"/> / <see cref="End"/> 都<b>不含</b>出手抬高量，都是贴地逻辑位置；<see cref="End"/> 是唯一可信的落点真值。</remarks>
    public readonly struct ProjectileTrajectory
    {
        public readonly BallType Type;

        public readonly Vector2 Start;

        public readonly Vector2 End;

        /// <summary>飞行总时长（秒），已按飞行距离缩放。</summary>
        public readonly float Duration;

        public readonly float MaxHeight;

        /// <remarks><b>目标点仍有下限：</b>离 <paramref name="origin"/> 小于 <c>ProjectileSpec.MinThrowDistance</c> 时会被推开到该距离（鼠标压在玩家身上时方向向量为零，归一化会产生 <c>NaN</c>，球会消失或闪成非数坐标）；这条保底是本类型的契约，任何调用方都得遵守。</remarks>
        public ProjectileTrajectory(BallType type, Vector2 origin, Vector2 target, float distance, ProjectileSpec spec)
        {
            float minDistance = spec.MinThrowDistance;

            Vector2 towards = target - origin;
            float actual = towards.magnitude;

            if (actual < minDistance)
            {
                // 距离为 0 时方向取"右"：给一个确定的方向，避免除以零产生 NaN。
                Vector2 direction = actual > 1e-6f ? towards / actual : Vector2.right;

                target = origin + direction * minDistance;
                distance = minDistance;
            }

            Type = type;
            Start = origin;
            End = target;
            MaxHeight = spec.MaxHeight;

            // 按最远距离归一化：最远一投恰好是 FlightDuration 秒；外层 Max 防"传进来的距离为 0 或负"把时长变成 0（那会让球在首帧就落地）。
            Duration = spec.FlightDuration * (Mathf.Max(distance, minDistance) / spec.MaxThrowDistance);
        }

        /// <param name="t">已飞比例；调用方负责夹到 0..1（越界那一帧直接结算落地）。</param>
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
