using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 运行期生成的两张纯色 sprite：圆点与圆环。
    /// </summary>
    /// <remarks>
    /// <b>为什么不直接 <c>new SpriteRenderer { sprite = null }</c>：</b>没有 sprite 的 <c>SpriteRenderer</c>
    /// 渲染的是"默认白色方块的网格"，不受 <c>Draw Mode</c> / <c>Size</c> 控制，只受 <c>transform.localScale</c>
    /// 控制 —— 而 <c>transform.localScale</c> 同时又是 <c>BallData.SampleVisual()</c> 的输出载体，
    /// 两者会互相覆盖。生成贴图是唯一能让"视觉高度"与"视觉大小"各自独立的做法，
    /// 且白模不导入任何美术、不依赖 <c>Resources</c>，需求书第六节的"纯色色块，不导入美术"仍然成立。
    /// <para><b>具名 <c>pixelsPerUnit</c></b>（而不是留 100）：让"半径几米"与 <c>localScale</c> 的换算是一个确定的数，
    /// 否则调一次贴图尺寸，全场的球大小都会跟着变。</para>
    /// <para><b><c>HideFlags.HideAndDontSave</c></b>：运行期生成的对象若能被
    /// <c>Resources.UnloadUnusedAssets()</c>（切场景时自动跑）回收，sprite 会变成白块或直接消失。
    /// 加这个标记就不会被当作"未使用资源"。</para>
    /// </remarks>
    public static class PrimitiveSprites
    {
        private static Sprite _circle;

        /// <summary>实心圆点 sprite（直径 = 1 世界单位）。惰性生成，全工程共用一张。</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle();
                return _circle;
            }
        }

        /// <summary>
        /// 造一个<b>贴在</b>地面上的圆（或圆环）sprite，形状就是"一个圆做视觉透视压扁"的效果。
        /// </summary>
        /// <param name="radius">贴地半径（世界单位）。形状按这个尺寸烘进贴图。</param>
        /// <param name="solid"><c>true</c> = 实心圆盘（阴影）；<c>false</c> = 圆环（指示器、瞬闪）。</param>
        /// <remarks>
        /// 落点指示器、球阴影、落地瞬闪<b>共用这一个函数</b>，所以三者的"贴地感"必然一致。
        /// 谁要单独调整外观，改 <see cref="ThrowConstants.AIM_VERTICAL_SQUASH"/> 等常量即可，不要各写一份画法 ——
        /// 各写一份的下场就是又出一次"左右断开"（见 <see cref="BuildGroundRing"/> 的说明）。
        /// <para><b>球本体不走这里。</b>球是一颗<b>球</b>，正上方投影本来就是正圆，
        /// 压扁它会让它看起来像躺在地上的药丸。所以 <see cref="Circle"/> 保持正圆不动。</para>
        /// <para>返回的 sprite 半径恰好等于 <paramref name="radius"/>，缩放交给
        /// <see cref="ConfigureGround"/> 统一换算。</para>
        /// </remarks>
        public static Sprite GroundDiscOrRing(float radius, bool solid)
        {
            if (solid)
            {
                // 实心盘：内圈缩到 0，由 BuildGroundRing 的 solid 分支处理，不走环厚夹取。
                return BuildGroundRing(radius, ThrowConstants.AIM_VERTICAL_SQUASH,
                    ThrowConstants.AIM_PERSPECTIVE_TAPER, 1f, solid: true);
            }

            // 环厚按**世界单位**给再换算成比例：这样改半径不会顺带把环的粗细也改掉。
            float thickness = Mathf.Clamp(
                ThrowConstants.GROUND_RING_THICKNESS_METERS / Mathf.Max(radius, 1e-4f),
                0.02f, 0.9f);

            return BuildGroundRing(radius, ThrowConstants.AIM_VERTICAL_SQUASH,
                ThrowConstants.AIM_PERSPECTIVE_TAPER, thickness);
        }

        /// <summary>
        /// 配置一个纯色 sprite 渲染器。**所有视效件都走这里**，以保证 sprite / 排序 / 颜色三件事只写一遍。
        /// </summary>
        /// <param name="renderer">目标渲染器；<c>null</c> 时静默返回（视效缺失不该让逻辑炸掉）。</param>
        /// <param name="sprite">用哪张图。</param>
        /// <param name="color">颜色。</param>
        /// <param name="sortingOrder">排序层，见 <see cref="ThrowConstants.BALL_SORTING_ORDER"/>。</param>
        /// <param name="diameterMeters">期望的世界直径（米）。</param>
        public static void Configure(SpriteRenderer renderer, Sprite sprite, Color color, int sortingOrder, float diameterMeters)
        {
            if (renderer == null) return;

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            renderer.transform.localScale = new Vector3(diameterMeters, diameterMeters, 1f);
        }

        /// <summary>
        /// 把渲染器的缩放设成"贴图烘的半径"换算出的值，让贴地件的世界尺寸与 <paramref name="diameterMeters"/> 一致。
        /// </summary>
        /// <remarks>
        /// 贴地件的贴图是按自己的半径烘的（见 <see cref="GroundDiscOrRing"/>），
        /// 所以缩放系数 = 目标直径 / 贴图直径。与 <see cref="Configure"/> 分开是因为那条路假设"贴图直径 = 1 米"，
        /// 而贴地件的贴图直径随半径变。
        /// </remarks>
        public static void ConfigureGround(SpriteRenderer renderer, Sprite sprite, Color color, int sortingOrder, float bakedRadius, float diameterMeters)
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
            const int size = ThrowConstants.POINT_TEXTURE_SIZE;
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

                    // 不做抗锯齿：直接二值化。过采样由 UV 双线性过滤负责，白模不需要更精细的边缘。
                    bool inside = dx * dx + dy * dy <= radius * radius;

                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            return Commit(texture, pixels, size, "白模_圆点", ThrowConstants.POINT_PIXELS_PER_UNIT);
        }

        /// <summary>
        /// 生成贴地形状：一个圆做视觉透视后压扁到地面的效果（圆环或实心圆盘）。
        /// </summary>
        /// <param name="radius">水平半径（= sprite 宽度的一半，也就是这个圆原来的半径）。</param>
        /// <param name="verticalSquash">竖直压扁比例（1 = 不压扁，纯粹一个正圆）。</param>
        /// <param name="perspectiveTaper">上半弧相对下半弧再收窄的比例（0 = 上下对称的椭圆）。</param>
        /// <param name="thickness">环的厚度占半径的比例；<paramref name="solid"/> 为真时忽略。</param>
        /// <param name="solid"><c>true</c> = 实心圆盘；<c>false</c> = 圆环。</param>
        /// <remarks>
        /// <b>画法：先有一个圆，再整体压扁，最后把上半部收一点。</b>
        /// 上、下两半各自是一个<b>标准椭圆</b>，用各自的竖直半径：
        /// <code>
        ///   vBase = radius × verticalSquash      （下半弧）
        ///   vTop  = vBase × (1 − perspectiveTaper)（上半弧，更平）
        ///   (dx/rx)² + (dy/v)² ≤ 1   且  内圈同式 &gt; 1
        /// </code>
        /// 两半在左右最宽点 <c>(±radius, 0)</c> 相接 —— 那一点的切线<b>对任何椭圆都是竖直的</b>，
        /// 所以接缝处切向连续，不会出现断点。
        /// <para><b>为什么不是"竖直半径随 x 变化"：</b>那种写法（旧实现）在几何上根本不是椭圆 ——
        /// 它逐列算上下边界，左右两侧会被扭出一个豁口，实测逐行宽度会掉到 <c>0</c>，
        /// 表现为环的左右断开成两截。已用 ASCII 渲染逐行核对过，不再用那种写法。</para>
        /// <para>形状参数只是形状参数，<b>不参与任何落点计算</b>：不论环长什么样，落点都是同一个
        /// <c>ClampThrowPoint</c> 的输出。把观感和判定分开是刻意的 —— 否则改一次外观就得重验落点。</para>
        /// <para><b>边界保护</b>：<c>thickness</c> 收窄后内圈一旦不小于外圈，环就整个消失
        /// （而且不报错）。所以先把 <c>thickness</c> 夹到 <c>(0, 0.9)</c>；
        /// <c>verticalSquash</c> 与 <c>perspectiveTaper</c> 也各自夹住，避免除零与上下翻转。
        /// <b>实心盘不走那条夹取</b>：<c>solid</c> 时内圈直接缩到 0，否则中心会留一个 10% 的小洞，
        /// 画出来是甜甜圈。</para>
        /// </remarks>
        public static Sprite BuildGroundRing(float radius, float verticalSquash, float perspectiveTaper, float thickness, bool solid = false)
        {
            const int size = ThrowConstants.POINT_TEXTURE_SIZE;

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

            // 内圈 = 同形状按比例缩小 → 环厚沿周向基本均匀，比"减去一个固定像素宽"更好看。
            // solid 时内圈缩到 0：不是"厚度取 1 再靠 clamp 兜" —— 那样内圈还剩 10%，
            // 画出来是个**甜甜圈**而不是圆盘（中心会有一个小洞）。
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

                    // 椭圆隐式方程。外椭圆 &gt; 1 是椭圆外；环模式下再要求在内椭圆之外。
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

            return Commit(texture, pixels, size, "白模_地面环", pixelsPerUnit);
        }

        /// <summary>
        /// 把像素数组落成 sprite。
        /// </summary>
        /// <remarks>
        /// <c>alphaIsTransparency: true</c> 不是可有可无的：透明像素上残留的颜色会被双线性过滤带进边缘，
        /// 关掉它就会出现一圈脏边。这里一并把透明像素的 RGB 也清成 0，两层保险。
        /// </remarks>
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
                SpriteMeshType.FullRect
            );

            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;

            return sprite;
        }
    }
}
