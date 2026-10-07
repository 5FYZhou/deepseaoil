using UnityEngine;
using DeepseaOil.Data;
using DeepseaOil.Presentation.Primitive;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>球的阴影：贴<b>逻辑位置</b>（不含高度）走直线，并随高度缩小。</summary>
    /// <remarks>它是<b>贴地件，不进 Y-Sort 频带</b>：跟着 y 取档会让阴影随高度越过自己的主人；缩小是判断高度的第二重线索。</remarks>
    public sealed class BallShadow : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private ThrowTuning _tuning;

        /// <summary><see cref="PrimitiveSprites.ConfigureGround"/> 写进去的"完整尺寸"，<b>每帧只从它重算</b>。</summary>
        /// <remarks>不能每帧读回 <c>localScale</c> 当基准：那样读到的已经是上一帧收缩过的值，收缩会被逐帧累乘，一秒后阴影缩成一个点。这类缺陷不会报错、只是"看起来有点不对"，所以基准值必须单独存。</remarks>
        private Vector3 _baseScale = Vector3.one;

        /// <summary>建出 sprite 渲染器（由球驱动器调用，无需 inspector 接线）。</summary>
        /// <param name="sortingOrder">排序层，须低于球本体。</param>
        public void Initialize(ThrowTuning tuning, int sortingOrder, float diameterMeters, Color color)
        {
            _tuning = tuning;

            _renderer = gameObject.AddComponent<SpriteRenderer>();

            // 阴影是**贴在地上的圆盘**，所以走贴地件那套透视画法，与指示器、落地瞬闪同一份观感（球本体不走这条：压扁正圆投影会像躺在地上的药丸）。
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

        /// <param name="groundPos">贴地的逻辑位置。与 <see cref="BallView.Apply"/> 收到的必须是同一份。</param>
        /// <remarks><b>这里必须写世界坐标 <c>transform.position</c>，不能写 <c>localPosition</c>。</b>本组件的父物体是"球根物体"，而根物体是<b>悬空的</b>（在贴地位置之上再抬一个出手抬高量）：用 <c>localPosition</c> 会把那个抬高量一起继承过来，于是阴影也跟着离地、还跟着抛物线上下起伏 —— 这类缺陷不会报错，只表现为"阴影位置看着不对"。</remarks>
        public void Apply(Vector2 groundPos, float height, float maxHeight)
        {
            float offset = _tuning != null ? _tuning.groundVisualOffset : 0f;

            transform.position = new Vector3(groundPos.x, groundPos.y + offset, 0f);

            if (_renderer == null) return;

            // height 可能带着出手抬高量（球根物体的基准），先归一到 0..maxHeight 再决定收缩，否则"最高点时缩到多少"会随抬高量漂移。
            float peak = maxHeight > 0f ? maxHeight : 1f;
            float normalized = Mathf.Clamp01(height / peak);

            float atPeak = _tuning != null ? _tuning.shadowScaleAtPeak : 1f;
            float shrink = Mathf.Lerp(1f, atPeak, normalized);

            _renderer.transform.localScale = new Vector3(_baseScale.x * shrink, _baseScale.y * shrink, 1f);
        }
    }
}
