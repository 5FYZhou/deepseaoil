using UnityEngine;
using DeepseaOil.Data;
using DeepseaOil.Presentation.Primitive;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>球的阴影：贴地（不含高度）走直线，随高度缩小；不参与 Y-Sort</summary>
    public sealed class BallShadow : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private ThrowTuning _tuning;

        /// <summary>PrimitiveSprites.ConfigureGround 写入的完整尺寸，每帧只从它重算（读回 localScale 当基准会逐帧累乘，缩成一个点）</summary>
        private Vector3 _baseScale = Vector3.one;

        /// <summary>建出 sprite 渲染器，由球驱动器调用</summary>
        public void Initialize(ThrowTuning tuning, int sortingOrder, float diameterMeters, Color color)
        {
            _tuning = tuning;

            _renderer = gameObject.AddComponent<SpriteRenderer>();

            // 阴影是贴地圆盘，走贴地件那套透视画法，与指示器、落地瞬闪同一份观感
            float radius = diameterMeters * 0.5f;

            Sprite disc = PrimitiveSprites.GroundDiscOrRing(
                radius,
                tuning != null ? tuning.aimVerticalSquash : 1f,
                tuning != null ? tuning.aimPerspectiveTaper : 0f,
                tuning != null ? tuning.aimRingThicknessMeters : 0.07f,
                solid: true);

            PrimitiveSprites.ConfigureGround(_renderer, disc, color, sortingOrder, radius, diameterMeters);

            _baseScale = _renderer.transform.localScale;
        }

        /// <remarks>必须写世界坐标 transform.position：根物体悬空（贴地位置 + 出手抬高量），用 localPosition 会把抬高量继承给阴影</remarks>
        public void Apply(Vector2 groundPos, float height, float maxHeight)
        {
            float offset = _tuning != null ? _tuning.groundVisualOffset : 0f;

            transform.position = new Vector3(groundPos.x, groundPos.y + offset, 0f);

            if (_renderer == null) return;

            // height 可能带出手抬高量，先归一到 0..maxHeight 再收缩，否则最高点缩多少会随抬高量漂移
            float peak = maxHeight > 0f ? maxHeight : 1f;
            float normalized = Mathf.Clamp01(height / peak);

            float atPeak = _tuning != null ? _tuning.shadowScaleAtPeak : 1f;
            float shrink = Mathf.Lerp(1f, atPeak, normalized);

            _renderer.transform.localScale = new Vector3(_baseScale.x * shrink, _baseScale.y * shrink, 1f);
        }
    }
}
