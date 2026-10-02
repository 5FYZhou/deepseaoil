using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 俯视角活动区域：以场景里一个矩形碰撞体为唯一真值源，玩家钳位与相机 confiner 共用同一个框。
    /// </summary>
    /// <remarks>
    /// <b>未接线是安全降级，不是错误</b>：collider 为空或尺寸为 0 时 <see cref="IsValid"/> 为 false，
    /// <see cref="TryClamp"/> 原样返回、不钳位。若改成无条件 <c>Mathf.Clamp</c>，
    /// 场景里忘接线时玩家会被钉死在地图原点——现象诡异且难查。
    /// 调试面板显示“世界边界: 已启用 / 未接线”，接线成败一眼可见。
    /// </remarks>
    public readonly struct BoundsArea
    {
        private readonly BoxCollider2D _area;

        /// <param name="area">场景里覆盖可行走区域的矩形碰撞体；可为 null。</param>
        public BoundsArea(BoxCollider2D area)
        {
            _area = area;
        }

        /// <summary>区域是否可用：碰撞体存在、已启用，且尺寸两轴都非零。</summary>
        /// <remarks>
        /// 三个条件缺一不可：<c>enabled</c> 为假时 <c>bounds</c> 不再代表任何真实区域，
        /// 只看 <c>size</c> 会把"停用的框"当成有效区域——那样玩家会被钳到一个已经不存在的矩形里。
        /// </remarks>
        public bool IsValid => _area != null && _area.enabled && _area.size.x > 0f && _area.size.y > 0f;

        /// <summary>世界矩形；<see cref="IsValid"/> 为 false 时是 <c>default</c>，调用方须先判。</summary>
        public Bounds World => _area == null ? default : _area.bounds;

        /// <summary>
        /// 把位置钳进活动区域内。
        /// </summary>
        /// <returns>区域无效或位置原本就在内时返回 false（clamped 为原值）；发生钳位返回 true。</returns>
        public bool TryClamp(Vector2 position, out Vector2 clamped)
        {
            if (!IsValid)
            {
                clamped = position;
                return false;
            }

            // IsValid 已保证 size 两轴为正，故 min < max，Clamp 的两支都成立。
            Bounds b = _area.bounds;
            clamped = new Vector2(
                Mathf.Clamp(position.x, b.min.x, b.max.x),
                Mathf.Clamp(position.y, b.min.y, b.max.y)
            );

            return clamped != position;
        }
    }
}