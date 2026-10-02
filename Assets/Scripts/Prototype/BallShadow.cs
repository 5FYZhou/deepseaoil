using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 球的阴影：贴<b>逻辑位置</b>（不含高度）走直线，并随高度缩小。
    /// </summary>
    /// <remarks>
    /// <b>阴影是这个白模里唯一能让人相信"球在天上"的东西。</b>没有它，抛物线只是一块色块在屏幕上平移；
    /// 有了它，"球体在弧线上、阴影在直线上、两者在落地瞬间重合"这条信息才成立。
    /// <para>阴影贴的是逻辑位置、不是视觉位置 —— 这是"伪高度"方案的全部要点：世界坐标里球的真位置
    /// 一直是地面上那条直线，抛物线只是画出来的偏移。所以落地判定不需要碰撞检测。</para>
    /// <para>缩小（<see cref="ThrowConstants.SHADOW_SCALE_AT_PEAK"/>）是第二重线索：
    /// 只靠"球上去了"还不足以判断高度，阴影同步收缩才像光从正上方打下来。</para>
    /// </remarks>
    public sealed class BallShadow : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        /// <summary>
        /// <see cref="PrimitiveSprites.Configure"/> 写进去的"完整尺寸"，<b>每帧只从它重算</b>。
        /// </summary>
        /// <remarks>
        /// 不能每帧读回 <c>localScale</c> 当基准：那样读到的已经是上一帧收缩过的值，收缩会被逐帧累乘，
        /// 一秒后阴影缩成一个点。这类缺陷不会报错、只是"看起来有点不对"，所以基准值必须单独存。
        /// </remarks>
        private Vector3 _baseScale = Vector3.one;

        /// <summary>阴影相对地面的固定视觉偏移。</summary>
        private static readonly float GroundVisualOffset = ThrowConstants.GROUND_VISUAL_OFFSET;

        /// <summary>建出 sprite 渲染器（由 <see cref="ThrowSpawner"/> 调用，无需 inspector 接线）。</summary>
        /// <param name="sortingOrder">排序层，须低于球本体。</param>
        /// <param name="diameterMeters">阴影直径（米）。</param>
        /// <param name="color">阴影色。</param>
        public void Initialize(int sortingOrder, float diameterMeters, Color color)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();

            // 阴影是**贴在地上的圆盘**，所以走贴地件那套透视画法，与指示器、落地瞬闪同一份观感。
            // （球本体不走这条：球是球，正上方投影本来就是正圆 —— 压扁它会像躺在地上的药丸。）
            float radius = diameterMeters * 0.5f;
            Sprite disc = PrimitiveSprites.GroundDiscOrRing(radius, solid: true);

            PrimitiveSprites.ConfigureGround(_renderer, disc, color, sortingOrder, radius, diameterMeters);

            // 记下完整尺寸当基准；此后 Apply 每次都从它重算，不读回自己的输出。
            _baseScale = _renderer.transform.localScale;
        }

        /// <summary>
        /// 摆位并缩放。
        /// </summary>
        /// <param name="groundPos">贴地的逻辑位置。与 <see cref="BallView.Apply"/> 收到的必须是同一份。</param>
        /// <param name="height">当前弧高（越接近最高点，阴影越小、越淡）。</param>
        /// <remarks>
        /// <b>这里必须写世界坐标 <c>transform.position</c>，不能写 <c>localPosition</c>。</b>
        /// 本组件的父物体是"球根物体"，而根物体是<b>悬空的</b>（在贴地位置之上再抬一个
        /// <see cref="ThrowConstants.THROW_ORIGIN_HEIGHT"/>）。用 <c>localPosition</c> 就会把那个抬高量
        /// 一起继承过来，于是阴影也跟着离地、还跟着抛物线上下起伏 ——
        /// 而需求要的是"阴影贴地走直线"。这类缺陷不会报错，只表现为"阴影位置看着不对"。
        /// </remarks>
        public void Apply(Vector2 groundPos, float height)
        {
            transform.position = new Vector3(groundPos.x, groundPos.y + GroundVisualOffset, 0f);

            if (_renderer == null) return;

            // height 可能带着抬高量（球根物体的基准），先归一到 0..MAX_HEIGHT 再决定收缩，
            // 否则"最高点时缩到多少"会随抬高量漂移。
            float normalized = Mathf.Clamp01(height / ThrowConstants.MAX_HEIGHT);
            float shrink = Mathf.Lerp(1f, ThrowConstants.SHADOW_SCALE_AT_PEAK, normalized);

            _renderer.transform.localScale = new Vector3(_baseScale.x * shrink, _baseScale.y * shrink, 1f);
        }
    }
}
