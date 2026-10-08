using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>球本体：只负责把视觉位置摆到该在的地方</summary>
    /// <remarks>层次由 BallDriver 建：球根=逻辑位置，BallVisual=宿主（本地 y 承载伪高度）。Apply 重建 position，无累积漂移。</remarks>
    public sealed class BallView : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        public float Height { get; private set; }

        /// <summary>建子物体并配好 sprite，直径单位为米</summary>
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

        /// <summary>摆位：height=当前弧高，0=贴地</summary>
        public void Apply(Vector2 groundPos, float height)
        {
            Height = height;
            transform.position = new Vector3(groundPos.x, groundPos.y + height, 0f);

            // Y-Sort 档位取贴地 y（不是弧高）：排序回答它落在场上哪里
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
