using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 球本体：只负责"把视觉位置摆到该在的地方"，不知道时间、不知道落点。
    /// </summary>
    /// <remarks>
    /// 层次结构（<see cref="BallDriver"/> 建，白模不依赖 prefab）：
    /// <code>
    /// 球根物体 ── 逻辑位置（贴地，不含高度）
    /// ├── BallShadow（阴影，贴逻辑位置）
    /// └── BallVisual（本组件的宿主，本地 y 承载伪高度）
    ///     └── 球 sprite
    /// </code>
    /// 为什么要多一层 <c>BallVisual</c> 而不是直接抬 sprite：这样 <c>SpriteRenderer.localScale</c>
    /// 只表示"球多大"一件事。<c>BallDriver.Apply</c> 每次都<b>重建</b> <c>localPosition</c>（而不是
    /// <c>+= delta</c>），所以不存在累积漂移，"逻辑位置 + 高度偏移"这个式子每一帧都是完整真值。
    /// </remarks>
    public sealed class BallView : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        /// <summary>球 sprite 的中心相对逻辑位置的高度偏移；<c>0</c> 即"球贴在地面上"。</summary>
        public float Height { get; private set; }

        /// <summary>
        /// 建出子物体。参数由组合根（<see cref="ThrowSpawner"/>）给出，所以本类不需要 inspector 接线。
        /// </summary>
        /// <param name="sortingOrder">排序层，直接由 <see cref="ThrowConstants.BALL_SORTING_ORDER"/> 给出。</param>
        /// <param name="diameterMeters">球的视觉直径（米）。</param>
        /// <param name="color">球色，由 <see cref="BallType"/> 决定。</param>
        public void Initialize(int sortingOrder, float diameterMeters, Color color)
        {
            _renderer = CreateSprite();

            PrimitiveSprites.Configure(_renderer, PrimitiveSprites.Circle, color, sortingOrder, diameterMeters);
        }

        /// <summary>摆位。与 <see cref="BallShadow.Apply"/> 成对调用，两者吃的是<b>同一份</b> <paramref name="groundPos"/>。</summary>
        /// <param name="groundPos">逻辑位置（贴地）。</param>
        /// <param name="height">当前弧高，<c>0</c> 表示贴地。</param>
        public void Apply(Vector2 groundPos, float height)
        {
            Height = height;
            transform.position = new Vector3(groundPos.x, groundPos.y + height, 0f);
        }

        /// <summary>
        /// 造一个只有 <see cref="SpriteRenderer"/> 的子物体。
        /// </summary>
        /// <remarks>
        /// 用 <c>new GameObject</c> 而不是 <c>Instantiate</c>：不需要预制体，就不会有"预制体丢引用"
        /// 这一类需要人工排查的失败模式。分层只为让 Hierarchy 里一眼能看出谁是谁。
        /// </remarks>
        private SpriteRenderer CreateSprite()
        {
            var go = new GameObject("球 sprite");
            go.layer = ThrowConstants.OVERLAY_LAYER;
            go.transform.SetParent(transform, false);

            return go.AddComponent<SpriteRenderer>();
        }
    }
}
