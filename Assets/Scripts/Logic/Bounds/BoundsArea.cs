using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 地图活动区域：一份<b>纯数据</b>矩形。玩家钳位与相机 confiner 共用它代表的同一块地图。
    /// </summary>
    /// <remarks>
    /// <b>为什么不用 <c>Bounds</c> / <c>BoxCollider2D</c></b>：本类型住在逻辑层，而逻辑层的硬约束是
    /// 「零引擎类型」（见 <c>Docs/分层设计/逻辑层.md</c> §1.1）。<c>Bounds</c> 是 <c>UnityEngine</c> 类型，
    /// <c>BoxCollider2D</c> 更带着整个物理模块——一旦出现在这里，逻辑层就从"纯 C#"退化成"必须挂物理组件才能单测"。
    /// 因此本类型只存两个 <see cref="Vector2"/>（<c>Vector2</c> 是纯数学结构，与 <c>Mathf</c> 同属可接受的例外），
    /// <b>由组合根在表现层把 <c>BoxCollider2D</c> 折算成 min/max 后传进来</b>（见 <c>PlayerController</c>）。
    /// <para><b>未接线是安全降级，不是错误</b>：默认值（min == max == 零）判为无效，
    /// <see cref="TryClamp"/> 原样返回、不钳位。若改成无条件 <c>Mathf.Clamp</c>，
    /// 场景里忘接线时玩家会被钉死在地图原点——现象诡异且难查。
    /// 调试面板显示「世界边界: 已启用 / 未接线」，接线成败一眼可见。</para>
    /// </remarks>
    public readonly struct BoundsArea
    {
        /// <summary>矩形左下角（世界坐标，含）。</summary>
        public readonly Vector2 Min;

        /// <summary>矩形右上角（世界坐标，含）。</summary>
        public readonly Vector2 Max;

        /// <param name="min">左下角。</param>
        /// <param name="max">右上角；两轴都必须严格大于 <paramref name="min"/>，否则区域判为无效。</param>
        public BoundsArea(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>区域是否可用：两轴都有正尺寸。</summary>
        /// <remarks>
        /// 「尺寸为 0 的框」是最常见的误配：<c>BoxCollider2D</c> 被停用、或 Scene 视图里没设 Size 时，
        /// 它不再代表任何真实区域。只看"有没有传值"会把停用的框当成有效区域——
        /// 那样玩家会被钳到一个已经不存在的矩形里。
        /// </remarks>
        public bool IsValid => Max.x > Min.x && Max.y > Min.y;

        /// <summary>
        /// 把位置钳进活动区域内。
        /// </summary>
        /// <returns>区域无效、或位置原本就在内时返回 false（<paramref name="clamped"/> 为原值）；发生钳位返回 true。</returns>
        /// <remarks>
        /// 判定用 <c>sqrMagnitude</c> 而不是 <c>Vector2 !=</c>：后者带 1e-5 量级的容差，
        /// 会把"只差 1e-6 的钳位结果"报成未钳位。这里要的是"到底有没有动过"，
        /// 精确的平方距离（阈值 0 即"逐位相同"）语义更干净。
        /// </remarks>
        public bool TryClamp(Vector2 position, out Vector2 clamped)
        {
            if (!IsValid)
            {
                clamped = position;
                return false;
            }

            // IsValid 已保证两轴 min < max，故 Clamp 的两支都成立。
            clamped = new Vector2(
                Mathf.Clamp(position.x, Min.x, Max.x),
                Mathf.Clamp(position.y, Min.y, Max.y)
            );

            return (clamped - position).sqrMagnitude > 0f;
        }
    }
}
