using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>地图活动区域：一份纯数据矩形；玩家钳位与相机 confiner 共用它代表的同一块地图，min/max 由组合根在表现层把 <c>BoxCollider2D</c> 折算后传进来（见 <c>PlayerController</c>）。</summary>
    /// <remarks>
    /// 未接线是安全降级而非错误：默认值（min == max == 零，常见于框被停用 / 没设 Size）判为无效、<see cref="TryClamp"/> 原样返回不钳位；改成无条件 <c>Clamp</c> 会让忘接线时玩家被钉死在地图原点，现象诡异且难查。
    /// </remarks>
    public readonly struct BoundsArea
    {
        public readonly Vector2 Min;

        public readonly Vector2 Max;

        public BoundsArea(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        public bool IsValid => Max.x > Min.x && Max.y > Min.y;

        /// <returns>区域无效、或位置原本就在内时返回 <c>false</c>（<paramref name="clamped"/> 为原值），发生钳位返回 <c>true</c>；判定用 <c>sqrMagnitude</c> 而非 <c>Vector2 !=</c>（后者带 1e-5 量级容差，会把只差 1e-6 的钳位结果报成未钳位）。</returns>
        public bool TryClamp(Vector2 position, out Vector2 clamped)
        {
            if (!IsValid)
            {
                clamped = position;
                return false;
            }

            clamped = new Vector2(
                Mathf.Clamp(position.x, Min.x, Max.x),
                Mathf.Clamp(position.y, Min.y, Max.y)
            );

            return (clamped - position).sqrMagnitude > 0f;
        }
    }
}
