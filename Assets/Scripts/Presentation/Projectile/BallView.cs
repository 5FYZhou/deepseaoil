using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>球本体：只负责"把视觉位置摆到该在的地方"，不知道时间、不知道落点。</summary>
    /// <remarks>层次由 <see cref="BallDriver"/> 建（不依赖 prefab）：球根＝逻辑位置（贴地），BallVisual＝本组件宿主（本地 y 承载伪高度）。<see cref="Apply"/> 每次重建 <c>position</c>（而不是 <c>+= delta</c>），所以不存在累积漂移。</remarks>
    public sealed class BallView : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        public float Height { get; private set; }

        /// <summary>建出子物体并配好 sprite（视觉直径单位为米）；排序档位不在这里定 —— 球每帧按贴地 y 取档（见 <see cref="Apply"/>）。</summary>
        public void Initialize(float diameterMeters, Color color)
        {
            _renderer = CreateSprite();

            PrimitiveSprites.Configure(
                _renderer,
                PrimitiveSprites.Circle,
                color,
                RenderOrder.YSortBandStart,
                diameterMeters);
        }

        /// <summary>摆位，与 <see cref="BallShadow.Apply"/> 成对调用（两者吃的是同一份贴地位置）；<paramref name="height"/> 为当前弧高，<c>0</c> 表示贴地。</summary>
        public void Apply(Vector2 groundPos, float height)
        {
            Height = height;
            transform.position = new Vector3(groundPos.x, groundPos.y + height, 0f);

            // Y-Sort 档位取贴地位置的 y（不是弧线高度）：高度是画出来的偏移，排序回答的是"它落在场上的哪里"。
            if (_renderer != null) _renderer.sortingOrder = RenderOrder.BallOrder(groundPos.y);
        }

        private SpriteRenderer CreateSprite()
        {
            var go = new GameObject("BallSprite");

            go.layer = RenderOrder.OverlayLayer;
            go.transform.SetParent(transform, false);

            return go.AddComponent<SpriteRenderer>();
        }
    }
}
