using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Effects;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>瞄准格高亮的表现侧订阅者：听逻辑层 AimChanged，转成一次特效调用</summary>
    /// <remarks>色源是观感表（ConfigModule.Visuals），不是敌人的取值边界；持续效果创建一次，之后每帧只更新位置与颜色，不瞄了就 Stop。</remarks>
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

        public void Attach()
        {
            if (_attached) return;

            EventBus<AimChanged>.Subscribe(OnAimChanged);
            _attached = true;
        }

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

            // 半径=半格：驱动的契约是"半径"，画出来正好一格。
            ctx.Radius = _cellSize * 0.5f;
            ctx.Tint = evt.Available ? AvailableColor() : BlockedColor();

            // 句柄失效（CleanAll/驱动换过）时重新创建
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
