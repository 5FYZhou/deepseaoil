using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 落地瞬闪：在落点画出<b>一个或两个同心环</b>，各自从满尺寸缩到消失。
    /// </summary>
    /// <remarks>
    /// <b>它不是特效，是仪表。</b>落地的作用范围在屏幕上原本不可见 —— 没有这个圈，
    /// 试冲量强度就只能靠反复猜。闪一圈的半径就是那件事的实际生效范围。
    /// 需求书第四节把"音效、粒子、拖尾"列为不做，本类不属于那一类：它不表达情绪，只暴露数值。
    /// <para><b>为什么是两个环：</b>一粒水球同时造成两件范围不同的事 ——
    /// 击退（半径 <see cref="ThrowConstants.IMPULSE_RADIUS"/> = 1.2）与泥浆（半径
    /// <see cref="ThrowConstants.MUD_RADIUS_METERS"/> = 1.8）。只画一个环时，
    /// "圈是 1.2 但泥浆铺到 1.8"看起来就是**指示范围与实际范围对不上**。
    /// 两个环把这件事显式画出来：<b>画出来的每个半径都精确等于一件事的生效半径</b>。</para>
    /// <para>生命周期与球一致（<c>Instantiate</c> / <c>Destroy</c>，不上对象池）。</para>
    /// </remarks>
    public sealed class LandingFlash : MonoBehaviour
    {
        /// <summary>
        /// 圆环底图烘制用的基准半径（世界单位）。
        /// </summary>
        /// <remarks>
        /// 与 <c>MudPatch.BakedRadius</c> 同一个道理，也是同一个值：
        /// <b>固定按 1 米烘，让"缩放 = 直径"这条换算只有一个来源。</b>
        /// 曾经按"实际半径"烘、又在 <c>Apply</c> 里写 <c>直径</c> 当缩放 ——
        /// 那份缩放里混进了一个已经烘进贴图的量，只有在"烘的半径恰好等于要画的半径"时才对得上，
        /// 改一个参数就会让两个系数相乘、圈大出一个数量级（而且不报错）。
        /// </remarks>
        private const float BakedRadius = 1f;

        /// <summary><see cref="Initialize"/> 的入参上限：超过这个数量不建（白模最多两个）。</summary>
        private const int MaxRings = 2;

        private readonly SpriteRenderer[] _rings = new SpriteRenderer[MaxRings];
        private readonly float[] _radii = new float[MaxRings];
        private readonly float[] _alphas = new float[MaxRings];
        private readonly float[] _delays = new float[MaxRings];

        private int _ringCount;
        private float _duration;
        private float _elapsed;

        /// <summary>
        /// 建出环并记下参数。
        /// </summary>
        /// <param name="primaryRadius">第一个环的半径（世界单位）；通常传击退半径。</param>
        /// <param name="primaryColor">第一个环的颜色。</param>
        /// <param name="secondaryRadius">第二个环的半径；<b>传 ≤ 0 表示只要一个环</b>。</param>
        /// <param name="secondaryColor">第二个环的颜色。</param>
        /// <param name="secondaryDelay">第二个环比第一个晚出现的时长（秒）。</param>
        /// <param name="duration">总时长（秒）。非正数时退回 <see cref="ThrowConstants.LANDING_FLASH_DURATION"/>。</param>
        /// <remarks>
        /// 两个环都从<b>各自半径的满尺寸</b>缩到 0，所以任何一个时刻看到的最外沿都小于等于该环的半径 ——
        /// 也就是"圈只会比生效范围小，永远不会比它大"，不会出现"圈住了却没生效"。
        /// <para>第二个环晚出现是为了不让两个环看起来像一个粗环：延迟期间它不可见，
        /// 于是先看到击退圈、再看到外圈铺开。</para>
        /// </remarks>
        public void Initialize(
            float primaryRadius,
            Color primaryColor,
            float secondaryRadius,
            Color secondaryColor,
            float secondaryDelay,
            float duration)
        {
            // 从调用方给的时长派生"进度"，不另立一个时长常量 —— 否则两处时长会各说各话。
            _duration = duration > 0f ? duration : ThrowConstants.LANDING_FLASH_DURATION;

            _ringCount = 0;

            AddRing(primaryRadius, primaryColor, 0f);

            if (secondaryRadius > 0f) AddRing(secondaryRadius, secondaryColor, secondaryDelay);

            _elapsed = 0f;
        }

        private void AddRing(float radius, Color color, float delay)
        {
            if (_ringCount >= MaxRings) return;

            var go = new GameObject($"环_{radius:F2}");

            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();

            // 半透明：它是地面的一个"标记"，不该在那一帧把球和角色全盖住。
            float alpha = color.a * 0.75f;

            color.a = alpha;

            // 与落点指示器、泥浆共用同一份贴地画法：三者对"贴地"的理解必须一致。
            Sprite ring = PrimitiveSprites.GroundDiscOrRing(BakedRadius, solid: false);

            PrimitiveSprites.ConfigureGround(
                renderer,
                ring,
                color,
                ThrowConstants.LANDING_FLASH_SORTING_ORDER,
                BakedRadius,
                radius * 2f
                );

            _rings[_ringCount] = renderer;
            _radii[_ringCount] = radius;
            _alphas[_ringCount] = alpha;
            _delays[_ringCount] = delay;
            _ringCount++;

            Apply();
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            Apply();

            if (_elapsed >= _duration) Destroy(gameObject);
        }

        /// <summary>按当前进度摆每个环的尺寸与透明度。</summary>
        private void Apply()
        {
            for (int i = 0; i < _ringCount; i++)
            {
                SpriteRenderer renderer = _rings[i];

                if (renderer == null) continue;

                // 延迟期内不出现：进度留在 1（满尺寸），但整体不可见。
                float remaining = Mathf.Clamp01(1f - (_elapsed - _delays[i]) / _duration);

                if (_elapsed < _delays[i]) remaining = 0f;

                float diameter = _radii[i] * remaining * 2f;

                renderer.transform.localScale = new Vector3(diameter, diameter, 1f);

                Color c = renderer.color;

                renderer.color = new Color(c.r, c.g, c.b, _alphas[i] * remaining);
            }
        }
    }
}
