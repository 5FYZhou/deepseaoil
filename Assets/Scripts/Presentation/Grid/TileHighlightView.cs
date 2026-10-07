using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Effects;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>瞄准格高亮的<b>表现侧订阅者</b>：听逻辑层发布的 <see cref="AimChanged"/>，转成一次特效调用。</summary>
    /// <remarks>
    /// 色源是观感表（<c>ConfigModule.Visuals</c>），<b>不是敌人的取值边界</b>；"高亮长什么样"归特效系统（<c>EffectId.Highlight</c> 的驱动自持资产）。
    /// 订阅时机由组合根收口：装配期调 <see cref="Attach"/>、销毁期调 <see cref="Detach"/>。
    /// 持续效果：创建一次（<c>Play</c>），之后每帧只 <c>Update</c> 位置与颜色，不瞄了就 <c>Stop</c>。
    /// </remarks>
    public sealed class TileHighlightView : MonoBehaviour
    {
        private GridGeometry _geometry;

        private float _cellSize = 1f;

        private EffectHandle _handle;

        private bool _attached;

        public bool IsVisible => _handle.IsValid;

        public void Initialize(in GridGeometry geometry, float cellSize)
        {
            _geometry = geometry;
            _cellSize = cellSize > 0f && !float.IsNaN(cellSize) ? cellSize : 1f;
        }

        /// <summary>开始听（由组合根在装配期调；可重复调用）。</summary>
        public void Attach()
        {
            if (_attached) return;

            EventBus<AimChanged>.Subscribe(OnAimChanged);
            _attached = true;
        }

        /// <summary>停止听并收掉高亮（由组合根在销毁期调；可重复调用）。</summary>
        public void Detach()
        {
            if (_attached)
            {
                EventBus<AimChanged>.Unsubscribe(OnAimChanged);
                _attached = false;
            }

            Hide();
        }

        private void OnAimChanged(AimChanged evt)
        {
            if (!evt.HasAim || !_geometry.IsValid)
            {
                Hide();
                return;
            }

            EffectContext ctx = EffectContext.At(_geometry.CellCenter(evt.Cell));

            // 半径 = 半格：驱动的契约是"半径"（与落地环一致），画出来正好一格。
            ctx.Radius = _cellSize * 0.5f;
            ctx.Tint = evt.Available ? AvailableColor() : BlockedColor();

            // 已经在播就只更新；句柄失效（CleanAll / 驱动换过）时重新创建
            if (_handle.IsValid && EffectModule.Update(_handle, in ctx)) return;

            _handle = EffectModule.Play(EffectId.Highlight, in ctx);
        }

        private static Color AvailableColor()
        {
            return ConfigModule.Visuals.highlightAvailable;
        }

        private static Color BlockedColor()
        {
            return ConfigModule.Visuals.highlightBlocked;
        }

        private void Hide()
        {
            if (!_handle.IsValid) return;

            EffectModule.Stop(_handle);

            _handle = EffectHandle.None;
        }
    }
}
