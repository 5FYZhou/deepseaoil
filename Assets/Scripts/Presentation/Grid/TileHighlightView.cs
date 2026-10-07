using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Presentation.Effects;
using UnityEngine;

namespace DeepseaOil.Presentation.Grid
{
    /// <summary>
    /// 瞄准格高亮的<b>表现侧订阅者</b>：听逻辑层发布的 <see cref="AimChanged"/>，转成一次特效调用。
    /// </summary>
    /// <remarks>
    /// <b>它替代了白模件 <c>TileAimView</c></b>（那个 MonoBehaviour 自己画色块、自己存 SpriteRenderer）。
    /// 现在"高亮长什么样"归特效系统（<c>EffectId.Highlight</c> 的驱动自持资产），
    /// 本类只剩一件职责：<b>把逻辑层的事实翻译成特效调用</b>。
    /// <para><b>订阅时机由组合根收口</b>：它不自己 <c>OnEnable</c> 订阅，而是由 <c>CombatRoot</c>
    /// 在装配期调 <see cref="Attach"/>、销毁期调 <see cref="Detach"/> ——
    /// 于是"什么时候开始听、什么时候停止听"只有一个答案。</para>
    /// <para><b>持续效果</b>：创建一次（<c>Play</c>），之后每帧只 <c>Update</c> 位置与颜色，
    /// 不瞄了就 <c>Stop</c>。这是"持续效果口"在工程里的第一个调用点。</para>
    /// </remarks>
    public sealed class TileHighlightView : MonoBehaviour
    {
        /// <summary>兜底的两态色（观感表缺失时用）。</summary>
        private static readonly Color FallbackAvailable = new(1f, 1f, 1f, 0.32f);
        private static readonly Color FallbackBlocked = new(1f, 0.25f, 0.2f, 0.42f);

        private GridGeometry _geometry;

        /// <summary>格子边长（世界单位）。</summary>
        private float _cellSize = 1f;

        private EffectHandle _handle;

        private bool _attached;

        /// <summary>当前是否有高亮在显示（诊断 / 测试读数）。</summary>
        public bool IsVisible => _handle.IsValid;

        /// <summary>
        /// 装配：记下格子几何（"格 → 世界中心"的换算只有一份）。
        /// </summary>
        /// <param name="geometry">格子几何。</param>
        /// <param name="cellSize">格子边长；非法值按 1 处理。</param>
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

        /// <summary>可用色（白，半透明：它是提示不是物体）。</summary>
        private static Color AvailableColor()
        {
            VisualPalette visuals = ConfigModule.GetEnemy().Visuals;

            return visuals != null ? visuals.highlightAvailable : FallbackAvailable;
        }

        /// <summary>不可用色（红）。</summary>
        private static Color BlockedColor()
        {
            VisualPalette visuals = ConfigModule.GetEnemy().Visuals;

            return visuals != null ? visuals.highlightBlocked : FallbackBlocked;
        }

        private void Hide()
        {
            if (!_handle.IsValid) return;

            EffectModule.Stop(_handle);

            _handle = EffectHandle.None;
        }
    }
}
