using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>地图活动区域：纯数据矩形，玩家钳位与相机 confiner 共用</summary>
    /// <remarks>默认值（min==max==零）判无效，TryClamp 原样返回不钳位</remarks>
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

        /// <remarks>无效或已在内返回 false（clamped 为原值），钳位了返回 true；判定用 sqrMagnitude 而非 Vector2 !=，后者 1e-5 容差会把只差 1e-6 的钳位报成未钳位</remarks>
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
