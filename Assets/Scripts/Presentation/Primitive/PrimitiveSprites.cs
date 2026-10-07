using UnityEngine;

namespace DeepseaOil.Presentation.Primitive
{
    /// <summary>运行期生成的两张纯色 sprite：圆点与方块/贴地圆环。<b>缺美术资源时的 fallback</b>。</summary>
    /// <remarks>不给 <c>SpriteRenderer</c> 留空 sprite：没有 sprite 的渲染器画的是"默认白色方块"网格，只受 <c>transform.localScale</c> 控制 —— 而 <c>localScale</c> 同时是"球多大 / 阴影缩到多少"的载体，两者会互相覆盖。
    /// 具名 <c>pixelsPerUnit</c> 让"半径几米"与 <c>localScale</c> 的换算是一个确定的数，否则调一次贴图尺寸全场的球大小都会跟着变。<c>HideFlags.HideAndDontSave</c>：否则运行期生成的对象被 <c>Resources.UnloadUnusedAssets()</c>（切场景时自动跑）回收后，sprite 会变成白块或直接消失。</remarks>
    public static class PrimitiveSprites
    {
        private const int PointTextureSize = 64;

        private const float PointPixelsPerUnit = 64f;

        private static Sprite _circle;
        private static Sprite _square;

        /// <summary>实心圆点 sprite（直径 = 1 世界单位）。惰性生成，全工程共用一张。</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle();

                return _circle;
            }
        }

        /// <summary>实心方块 sprite（边长 = 1 世界单位），用于"整格"类提示（瞄准高亮）：压扁的圆与格子之间会留一圈空隙，"这一格"就读不出来了。</summary>
        public static Sprite Square
        {
            get
            {
                if (_square == null) _square = BuildSquare();

                return _square;
            }
        }

        /// <remarks>造一个<b>贴在</b>地面上的圆（或圆环）sprite：一个圆做视觉透视压扁后的效果。<paramref name="thicknessMeters"/> 是<b>世界单位</b>（<paramref name="solid"/> 为真时忽略）；<paramref name="verticalSquash"/> 1 = 不压扁，<paramref name="perspectiveTaper"/> 0 = 上下对称椭圆；返回的 sprite 半径恰好等于 <paramref name="radius"/>。落点指示器、球阴影、落地瞬闪共用这一个函数，所以"贴地感"必然一致；球本体走 <see cref="Circle"/>（保持正圆）。</remarks>
        public static Sprite GroundDiscOrRing(
            float radius,
            float verticalSquash,
            float perspectiveTaper,
            float thicknessMeters,
            bool solid)
        {
            // 环厚按世界单位给再换算成比例：改半径不会顺带把环的粗细也改掉。实心盘走内圈缩到 0 的分支。
            float thickness = solid
                ? 1f
                : Mathf.Clamp(thicknessMeters / Mathf.Max(radius, 1e-4f), 0.02f, 0.9f);

            return BuildGroundShape(radius, verticalSquash, perspectiveTaper, thickness, solid);
        }

        /// <summary>配置一个纯色 sprite 渲染器；**所有视效件都走这里**（sprite / 排序 / 颜色只写一遍）。</summary>
        /// <param name="renderer">目标渲染器；<c>null</c> 时静默返回（视效缺失不该让逻辑炸掉）。</param>
        public static void Configure(
            SpriteRenderer renderer,
            Sprite sprite,
            Color color,
            int sortingOrder,
            float diameterMeters)
        {
            if (renderer == null) return;

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            renderer.transform.localScale = new Vector3(diameterMeters, diameterMeters, 1f);
        }

        /// <remarks>把渲染器的缩放设成"贴图烘的半径"换算出的值，让贴地件的世界尺寸与 <paramref name="diameterMeters"/> 一致：缩放系数 = 目标直径 / 贴图直径（贴地件的贴图按自己的半径烘）；与 <see cref="Configure"/> 分开，是因为那条路假设"贴图直径 = 1 米"。</remarks>
        public static void ConfigureGround(
            SpriteRenderer renderer,
            Sprite sprite,
            Color color,
            int sortingOrder,
            float bakedRadius,
            float diameterMeters)
        {
            if (renderer == null) return;

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            float scale = diameterMeters / Mathf.Max(bakedRadius * 2f, 1e-4f);

            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static Sprite BuildCircle()
        {
            const int size = PointTextureSize;
            const float radius = size * 0.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // +0.5 取像素中心：否则 x=0 与 x=size-1 那两个像素到圆心的距离不一致，圆会差半像素。
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;

                    bool inside = dx * dx + dy * dy <= radius * radius;

                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return Commit(texture, pixels, size, "GroundDot", PointPixelsPerUnit);
        }

        private static Sprite BuildSquare()
        {
            const int size = 4;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            // 像素密度取 size ⇒ 贴图边长恰好 1 世界单位，与 Circle 的约定一致。
            return Commit(texture, pixels, size, "GroundSquare", size);
        }

        /// <remarks>生成贴地形状：一个圆做视觉透视后压扁到地面的效果（圆环或实心圆盘）。画法：上、下两半各自是一个标准椭圆（<c>vBase = radius × verticalSquash</c>；<c>vTop = vBase × (1 − perspectiveTaper)</c>），在左右最宽点的切线竖直 ⇒ 接缝无断点；<b>形状参数不参与任何落点计算</b>，落点都是同一个吸附函数的输出。
        /// <b>边界保护</b>：<c>thickness</c> 收窄后内圈一旦不小于外圈，环就整个消失（而且不报错）；<b>实心盘不走那条夹取</b>（内圈直接缩到 0），否则中心会留一个洞、画出来是甜甜圈。</remarks>
        public static Sprite BuildGroundShape(
            float radius,
            float verticalSquash,
            float perspectiveTaper,
            float thickness,
            bool solid = false)
        {
            const int size = PointTextureSize;

            thickness = Mathf.Clamp(thickness, 0.01f, 0.9f);
            verticalSquash = Mathf.Clamp(verticalSquash, 0.02f, 1f);
            perspectiveTaper = Mathf.Clamp(perspectiveTaper, 0f, 0.95f);

            // sprite 是正方形，所以用水平直径换算像素密度；竖直方向靠压扁比例体现。
            float pixelsPerUnit = size / Mathf.Max(radius * 2f, 1e-4f);

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float rx = size * 0.5f;

            float vBase = rx * verticalSquash;
            float vTop = vBase * (1f - perspectiveTaper);

            float innerScale = solid ? 0f : (1f - thickness);
            float ix = rx * innerScale;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;

                    // 屏幕 y 向上为正：dy > 0 是上半弧，用更平的那个竖直半径。
                    float v = dy >= 0f ? vTop : vBase;

                    float outer = (dx * dx) / (rx * rx) + (dy * dy) / (v * v);

                    bool inside = outer <= 1f;

                    if (inside && !solid)
                    {
                        float iy = v * innerScale;
                        float inner = (dx * dx) / (ix * ix) + (dy * dy) / (iy * iy);

                        inside = inner >= 1f;
                    }

                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return Commit(texture, pixels, size, "GroundShape", pixelsPerUnit);
        }

        /// <remarks>把像素数组落成 sprite。<c>alphaIsTransparency: true</c> 不是可有可无的：透明像素上残留的颜色会被双线性过滤带进边缘，关掉它就会出现一圈脏边。</remarks>
        private static Sprite Commit(Texture2D texture, Color32[] pixels, int size, string name, float pixelsPerUnit)
        {
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),        // pivot 居中：localScale 才是"以自身为中心"缩放
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);

            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }
    }
}
