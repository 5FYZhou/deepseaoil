using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Effects;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>瞄准格高亮：听 AimChanged，转成一次特效调用</summary>
    /// <remarks>色源是观感表（ConfigModule.Visuals）；持续效果创建一次，之后只更新位置与颜色。</remarks>
    public sealed class TileHighlightView : MonoBehaviour
    {
        private GridGeometry _geometry;

        private float _cellSize = 1f;

        private EffectHandle _handle;

        private bool _attached;

        // 持种时覆盖投掷裁决色
        private Color? _seedTintOverride;

        private bool _hasCell;

        private bool _aimAvailable;

        private Vector3Int _cell;

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
            _seedTintOverride = null;

            _hasCell = evt.HasAim && _geometry.IsValid;
            _aimAvailable = evt.Available;
            _cell = evt.Cell;

            if (!_hasCell)
            {
                Hide();
                return;
            }

            ApplyAt(_cell);
        }

        /// <summary>持种反馈：null = 交还投掷色</summary>
        public void SetSeedTint(bool? canPlant)
        {
            _seedTintOverride = canPlant.HasValue
                ? (canPlant.Value ? AvailableColor() : BlockedColor())
                : (Color?)null;

            if (!_hasCell) return;

            ApplyAt(_cell);
        }

        private void ApplyAt(Vector3Int cell)
        {
            EffectContext ctx = EffectContext.At(_geometry.CellCenter(cell));

            // 半径=半格：驱动的契约是"半径"，画出来正好一格
            ctx.Value = _cellSize * 0.5f;
            ctx.Tint = ResolveTint();

            _handle = EffectModule.Play(EffectId.Highlight, in ctx);
        }

        private Color ResolveTint()
        {
            if (_seedTintOverride.HasValue) return _seedTintOverride.Value;

            return _aimAvailable ? AvailableColor() : BlockedColor();
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
            _hasCell = false;

            if (!_handle.IsValid) return;

            EffectModule.Stop(_handle);

            _handle = EffectHandle.None;
        }
    }
}
