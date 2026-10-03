// ---------------------------------------------------------------------------
// 白模投掷 · EditMode 测试（临时资产）
//
// 【白模验收通过后整个文件删除。】它不是框架的一部分，是白模这一版的仪表盘。
//   单独删不掉的风险不大：删掉本文件、Assets/Scripts/Prototype/ 与 Assets/Scenes/TestThrow.unity
//   即为白模的全部痕迹（另有一行 Docs/目录说明.md）。
//   只想跑白模这几条：Test Runner ▸ EditMode ▸ 右侧分类筛选 WhiteBox。
//
// 【为什么在这里】Assets/Tests/Runtime/Editor/ —— 与 移动Tests.cs / Data层Tests.cs 同机制：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp（被测代码所在），又被 Test Framework 自动引用 NUnit。
//
// 【为什么是这些用例】白模投掷的风险集中在"说好的落点没落到"和"说好的弧高没有"，
// 两者都是错了不报错、只是手感不对：
//   W1  起点终点精确落地      —— 视觉高度在 t=1 必须恰好归零，否则球浮在空中或不落地
//   W2  视觉最高点等于常量    —— 只测 t=0.5 挡不住"换了曲线但不是抛物线"
//   W3  落点被夹到最远距离    —— 夹不准就是"看着能扔到、其实扔不到"
//   W4  落点不落在玩家脚下    —— 方向向量为零时归一化会产生 NaN，球会消失或闪成非数坐标
//   W5  时长按飞行距离缩放    —— 固定时长会让贴脸投慢得像飘
//   W6  指示器与球落点一致    —— **本文件的重点**：两处各算一次 clamp 必然会漂移
//   W7  非法上限必须被换掉    —— **真实缺陷**：NaN 上限会把 NaN 传染给落点，球带着 NaN 坐标凭空消失
//   W8  球种决定颜色          —— 左键出水球、右键出土球，反了是操作语义错
//   W9  白模数值互相自洽      —— 阴影比球大、指示器盖住球、冲量半径为零，全是运行时不报错的静默失效
//   W10 阴影走直线、球不沉地  —— **真实缺陷**：抬高量曾被烘进 Start/End，阴影跟着抛物线一起起伏
//   W11 贴地形状不得断/空洞   —— **真实缺陷**：旧画法不是椭圆，环左右断成两截；实心盘会变甜甜圈
//
// 【覆盖边界，写在明处】本文件只测 BallData 与 ThrowSpawner.ClampThrowPoint 这两个
//   **静态纯函数/纯数据**通路。不测 MonoBehaviour 生命周期、不测鼠标设备、不测渲染 ——
//   那些在白模阶段由人工 Play 手测（见 Docs/toAgent/白模接入.md 的验收清单）。
//   写假测试会把真绿变成"全绿但缺陷仍在"，移动Tests.cs 头部已记过这个教训。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using DeepseaOil.Prototype;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.Tests
{
    /// <summary>【临时】白模投掷测试。白模验收后整文件删除。</summary>
    [Category("WhiteBox")]
    public class 白模抛球Tests
    {
        /// <summary>球的"视觉高度"容差。曲线是多项式，可以用很小的容差。</summary>
        private const float HeightTolerance = 1e-4f;

        /// <summary>落点容差。落点经过归一化与乘法，容差略放宽。</summary>
        private const float PositionTolerance = 1e-3f;

        /// <summary>造一颗飞往 <paramref name="target"/> 的球，距离按 ThrowSpawner 的同一套算法算（模拟真实调用方）。</summary>
        private static BallData ThrowFrom(Vector2 origin, Vector2 target, BallType type)
        {
            Vector2 clamped = ThrowSpawner.ClampThrowPoint(origin, target, ThrowConstants.MAX_THROW_DISTANCE, out float distance);

            // 落点与距离都取自**同一次** clamp：这正是 ThrowSpawner.Throw 的写法。
            Assert.AreEqual(Vector2.Distance(origin, clamped), distance, PositionTolerance,
                "clamp 输出的落点与距离互相矛盾 —— 两者必须来自同一次计算");

            return new BallData(type, origin, clamped, distance);
        }

        // ================================================================
        // W1 · 抛物线起点终点精确落地
        // ================================================================

        [Test]
        public void W1_抛物线起点终点精确落地()
        {
            var origin = new Vector2(1f, 2f);
            var target = new Vector2(5f, 3.5f);
            BallData data = ThrowFrom(origin, target, BallType.Water);

            // 起点与落点都是**贴地的逻辑位置**，不含出手抬高量：阴影贴的就是它们。
            // 曾经的写法把抬高量烘进这两个字段，于是阴影也跟着离地（见 W10）。
            Assert.AreEqual(origin.x, data.Start.x, PositionTolerance, "起点 x 就是玩家位置");
            Assert.AreEqual(origin.y, data.Start.y, PositionTolerance, "起点 y 是贴地的，不该含出手抬高量");
            Assert.AreEqual(target.x, data.End.x, PositionTolerance, "落点 x 就是鼠标位置");
            Assert.AreEqual(target.y, data.End.y, PositionTolerance, "落点 y 是贴地的，不该含出手抬高量");

            Assert.AreEqual(data.Start.x, data.SampleGround(0f).x, PositionTolerance, "t=0 的逻辑位置就是起点");
            Assert.AreEqual(data.Start.y, data.SampleGround(0f).y, PositionTolerance, "t=0 的逻辑位置就是起点");

            Vector2 end = data.SampleGround(1f);
            Assert.AreEqual(data.End.x, end.x, PositionTolerance, "t=1 的逻辑位置就是落点");
            Assert.AreEqual(data.End.y, end.y, PositionTolerance, "t=1 的逻辑位置就是落点");

            // 抛物线相对地面线的视觉抬升：两端恰好归零、中点恰好 MAX_HEIGHT。
            // 少了这条，球会浮在空中不落地，或者一出手就看不见（陷进地面线以下）。
            Assert.AreEqual(0f, data.SampleVisual(0f).y - data.SampleGround(0f).y, HeightTolerance,
                "t=0 的视觉抬升必须恰好为 0，否则球一出手就浮着");
            Assert.AreEqual(0f, data.SampleVisual(1f).y - data.SampleGround(1f).y, HeightTolerance,
                "t=1 的视觉抬升必须恰好为 0，否则球落不到地面线上");
        }

        // ================================================================
        // W2 · 视觉最高点等于 MAX_HEIGHT
        // ================================================================

        [Test]
        public void W2_视觉最高点等于MAX_HEIGHT()
        {
            var origin = new Vector2(-2f, -1f);
            BallData data = ThrowFrom(origin, new Vector2(3f, -1f), BallType.Earth);

            float peak = 0f;
            float peakAt = 0f;

            // 用 1000 个采样点扫描，而不是只看 t=0.5：换成一个"顶峰不在中点"的曲线时，
            // 只测中点会漏掉，扫描才能把"它真的是一条抛物线"钉住。
            const int samples = 1000;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float height = data.SampleVisual(t).y - data.SampleGround(t).y;

                if (height > peak)
                {
                    peak = height;
                    peakAt = t;
                }
            }

            Assert.AreEqual(ThrowConstants.MAX_HEIGHT, peak, HeightTolerance,
                $"视觉最高点应当是 MAX_HEIGHT={ThrowConstants.MAX_HEIGHT}，实测 {peak}");
            Assert.AreEqual(0.5f, peakAt, 0.01f, $"最高点应当在中点，实测 t={peakAt}");
        }

        // ================================================================
        // W3 · 落点被夹到最远距离
        // ================================================================

        [Test]
        public void W3_落点被夹到最远距离()
        {
            var origin = new Vector2(3f, 3f);

            // 超远处：夹到**恰好**最远距离。
            Vector2 far = new Vector2(300f, 300f);
            Vector2 clamped = ThrowSpawner.ClampThrowPoint(origin, far, ThrowConstants.MAX_THROW_DISTANCE, out float farDistance);

            Assert.AreEqual(ThrowConstants.MAX_THROW_DISTANCE, farDistance, PositionTolerance,
                "超出最远距离时输出距离必须恰好等于 MAX_THROW_DISTANCE");
            Assert.AreEqual(ThrowConstants.MAX_THROW_DISTANCE, Vector2.Distance(origin, clamped), PositionTolerance,
                "夹出来的落点离出手点的距离必须恰好等于 MAX_THROW_DISTANCE");

            // 夹的是距离，不是坐标：方向必须原样保留，否则指示器会跑到鼠标的另一边。
            Vector2 expectedDirection = (far - origin).normalized;
            Vector2 actualDirection = (clamped - origin).normalized;
            Assert.Less(Vector2.Distance(expectedDirection, actualDirection), PositionTolerance,
                "夹距离时不得改动方向");

            // 范围内：一个字都不能动。
            var near = new Vector2(4f, 3f);
            Vector2 untouched = ThrowSpawner.ClampThrowPoint(origin, near, ThrowConstants.MAX_THROW_DISTANCE, out float nearDistance);
            Assert.AreEqual(near.x, untouched.x, PositionTolerance, "范围内的目标点必须原样返回");
            Assert.AreEqual(near.y, untouched.y, PositionTolerance, "范围内的目标点必须原样返回");
            Assert.AreEqual(1f, nearDistance, PositionTolerance, "输出的距离就是目标点到自己出手点的距离");
        }

        // ================================================================
        // W4 · 落点不落在玩家脚下
        // ================================================================

        [Test]
        public void W4_落点不落在玩家脚下()
        {
            var origin = new Vector2(-1.5f, 4f);

            for (int i = 0; i < 360; i += 1)
            {
                float radians = i * Mathf.Deg2Rad;
                var target = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 0.01f;   // 鼠标压在玩家身上

                BallData data = ThrowFrom(origin, target, BallType.Water);

                float distance = Vector2.Distance(data.Start, data.End);

                Assert.GreaterOrEqual(distance, ThrowConstants.MIN_THROW_DISTANCE - PositionTolerance,
                    $"角度 {i}° 的落点离出手点只有 {distance}，落到了脚下（方向为零会产生 NaN，球会消失）");

                // 方向必须是单位向量：本类型自己也要保证没除出 NaN 或无穷。
                Assert.IsFalse(float.IsNaN(data.End.x) || float.IsNaN(data.End.y),
                    $"角度 {i}° 的落点坐标是 NaN");
                Assert.IsFalse(float.IsInfinity(data.End.x) || float.IsInfinity(data.End.y),
                    $"角度 {i}° 的落点坐标是无穷");
            }
        }

        // ================================================================
        // W5 · 时长按飞行距离缩放
        // ================================================================

        [Test]
        public void W5_时长按飞行距离缩放()
        {
            var origin = Vector2.zero;

            BallData shortThrow = ThrowFrom(origin, new Vector2(2.5f, 0f), BallType.Water);
            BallData longThrow = ThrowFrom(origin, new Vector2(5f, 0f), BallType.Water);

            Assert.Greater(shortThrow.Duration, 0f, "时长必须是正数，0 会让球在首帧就落地");

            // 同一出手速度 → 时长与距离成正比。
            Assert.AreEqual(2f, longThrow.Duration / shortThrow.Duration, 1e-3f,
                "距离翻倍时时长必须翻倍（同一飞行速度）");

            // 最远一投恰好是 FLIGHT_DURATION。
            Assert.AreEqual(ThrowConstants.FLIGHT_DURATION, longThrow.Duration, PositionTolerance,
                "最远一投的时长应当恰好是 FLIGHT_DURATION");
        }

        // ================================================================
        // W6 · 指示器与球落点一致（本文件的重点）
        // ================================================================

        [Test]
        public void W6_指示器与球落点一致()
        {
            var origin = new Vector2(2f, -3f);

            // 覆盖"鼠标在近处""鼠标在最远处""鼠标远超最远处"三种情况。
            var targets = new[]
            {
                new Vector2(2.5f, -3f),
                new Vector2(7f, -3f),
                new Vector2(500f, 400f),
            };

            foreach (var target in targets)
            {
                // AimIndicator 画的那个点（只取位置，不要距离）。
                Vector2 aimed = ThrowSpawner.ClampThrowPoint(origin, target, ThrowConstants.MAX_THROW_DISTANCE, out _);

                // 球真正会落的那个点。
                BallData data = ThrowFrom(origin, target, BallType.Water);

                // 两者都是**贴地坐标**，可以直接比：出手抬高量与两者无关（它只在画球时叠加）。
                // 正因为这条比较不涉及抬高量，观感怎么改都不会让这条测试变红或变假绿。
                Assert.AreEqual(aimed.x, data.SampleGround(1f).x, PositionTolerance,
                    $"目标 {target}：指示器画的位置与球的落点 x 不一致 —— 玩家会看到「能扔到但扔不到」");
                Assert.AreEqual(aimed.y, data.SampleGround(1f).y, PositionTolerance,
                    $"目标 {target}：指示器画的位置与球的落点 y 不一致");
            }
        }

        // ================================================================
        // W7 · 数值非法时不得出 NaN
        // ================================================================

        /// <summary>
        /// 非法上限必须被换成默认最远距离。
        /// </summary>
        /// <remarks>
        /// <b><c>NaN</c> 那一档是真的红过。</b><c>NaN</c> 参与任何比较都是 <c>false</c>，所以
        /// <c>distance &lt;= NaN</c> 为假、函数一路走到乘法，把 <c>NaN</c> 传染给整个落点 ——
        /// 生成的球坐标全是 <c>NaN</c>，而它的 <c>t</c> 仍会推进并正常销毁，
        /// 表现只有"球凭空消失"，非常难从现象倒推原因。
        /// </remarks>
        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void W7_非法上限必须被换成默认值(float maxDistance)
        {
            var origin = new Vector2(1f, 1f);
            var target = new Vector2(9f, 9f);

            Assert.DoesNotThrow(() =>
            {
                Vector2 clamped = ThrowSpawner.ClampThrowPoint(origin, target, maxDistance, out float distance);

                Assert.IsFalse(float.IsNaN(clamped.x) || float.IsNaN(clamped.y),
                    $"maxDistance={maxDistance} 时落点是 NaN");
                Assert.IsFalse(float.IsNaN(distance), $"maxDistance={maxDistance} 时输出距离是 NaN");
                Assert.IsFalse(float.IsInfinity(clamped.x) || float.IsInfinity(clamped.y),
                    $"maxDistance={maxDistance} 时落点是无穷");

                // 换了默认值之后，行为必须与"直接传默认值"完全一致。
                Vector2 expected = ThrowSpawner.ClampThrowPoint(
                    origin, target, ThrowConstants.MAX_THROW_DISTANCE, out float expectedDistance);

                Assert.AreEqual(expectedDistance, distance, PositionTolerance,
                    $"maxDistance={maxDistance} 时应当退回默认最远距离");
                Assert.AreEqual(expected.x, clamped.x, PositionTolerance,
                    $"maxDistance={maxDistance} 时落点应当与默认值下的结果一致");
                Assert.AreEqual(expected.y, clamped.y, PositionTolerance,
                    $"maxDistance={maxDistance} 时落点应当与默认值下的结果一致");
            });
        }

        // ================================================================
        // W8 · 球种决定颜色
        // ================================================================

        [Test]
        public void W8_球种决定颜色()
        {
            Assert.AreNotEqual(ThrowConstants.WATER_BALL_COLOR, ThrowConstants.EARTH_BALL_COLOR,
                "水球与土球不能同色 —— 玩家就靠颜色区分左右键");
            Assert.AreNotEqual(Color.white, ThrowConstants.WATER_BALL_COLOR, "水球不该是白色");
            Assert.AreNotEqual(Color.white, ThrowConstants.EARTH_BALL_COLOR, "土球不该是白色");

            // 三个分量都必须在合法范围：写错成 0-255 会让颜色被钳成一坨。
            AssertColorInRange(ThrowConstants.WATER_BALL_COLOR, "水球色");
            AssertColorInRange(ThrowConstants.EARTH_BALL_COLOR, "土球色");
        }

        // ================================================================
        // W9 · 白模数值互相自洽
        // ================================================================

        /// <summary>
        /// 只查常量之间的关系，不查行为。写在这里是因为下面每一条错了都会**静默**失效
        /// （阴影比球大、指示器盖住球、冲量半径为零时无人可推），而它们全是运行时看不出来的。
        /// </summary>
        [Test]
        public void W9_白模数值互相自洽()
        {
            // 抬高量必须为正：为 0 时球半埋在地里，为负时球从地下钻出来。
            Assert.Greater(ThrowConstants.THROW_ORIGIN_HEIGHT, 0f, "抬高量必须为正，否则球半埋在地里");

            Assert.Greater(ThrowConstants.BALL_RADIUS_METERS, 0f, "球半径必须为正");
            Assert.Greater(ThrowConstants.SHADOW_RADIUS_METERS, 0f, "阴影半径必须为正");
            Assert.Greater(ThrowConstants.AIM_RADIUS_METERS, 0f, "指示器半径必须为正");

            // 阴影不该比球本体更大：俯视角下那看起来像两个物体。
            Assert.LessOrEqual(ThrowConstants.SHADOW_RADIUS_METERS, ThrowConstants.BALL_RADIUS_METERS,
                "阴影半径不该大于球本体半径");

            // 排序层：指示器 < 球 < 落地瞬闪。任何一条反过来都会出现"指示器盖住球"这类静默的观感缺陷。
            Assert.Less(ThrowConstants.AIM_SORTING_ORDER, ThrowConstants.BALL_SORTING_ORDER,
                "指示器必须画在球下面（排序层更小）");
            Assert.Less(ThrowConstants.BALL_SORTING_ORDER, ThrowConstants.LANDING_FLASH_SORTING_ORDER,
                "落地瞬闪必须画在球上面，否则落地那一下被球本体盖住");

            // 冲量半径必须大于 0，否则 OverlapCircleAll 永远查不到东西、冲量静默失效。
            Assert.Greater(ThrowConstants.IMPULSE_RADIUS, 0f, "冲量半径必须为正，否则冲量永远无人可推");
            Assert.Greater(ThrowConstants.IMPULSE_STRENGTH, 0f, "冲量强度必须为正");
            Assert.Greater(ThrowConstants.IMPULSE_MASS_FLOOR, 0f, "质量下限必须为正，否则质量趋零时速度趋于无穷");

            // 夹距离的下限必须严格小于上限，否则 clamp 区间是空的。
            Assert.Less(ThrowConstants.MIN_THROW_DISTANCE, ThrowConstants.MAX_THROW_DISTANCE,
                "最小投掷距离必须小于最大投掷距离");
        }

        // ================================================================
        // W10 · 阴影走直线、球走抛物线
        // ================================================================

        /// <summary>
        /// 阴影吃的是<b>贴地逻辑位置</b>（不含任何高度），球本体吃的是"逻辑位置 + 出手抬高量 + 弧高"。
        /// </summary>
        /// <remarks>
        /// <b>这一条是为一个真实缺陷写的。</b>曾经把出手抬高量烘进 <c>BallData.Start/End</c>，
        /// 而阴影是球根物体的子物体、又用 <c>localPosition</c> 定位，于是它把那个抬高量一起继承了：
        /// 阴影看起来"浮在球下面一点"，还跟着抛物线上下起伏，而不是贴地走直线。
        /// <para>本测试复算 <c>BallDriver.ApplyAt</c> 的推导（两处的公式必须一致）：
        /// 球相对地面的高度从出手抬高量起、前半程单调不减、顶点恰好是
        /// "抬高量 + MAX_HEIGHT"、落地回到出手抬高量。球的高度一旦低于抬高量，
        /// 就说明球沉进了地面（阴影比球还高）。</para>
        /// </remarks>
        [Test]
        public void W10_阴影走直线且球不沉地()
        {
            var origin = new Vector2(-1f, 0.5f);
            var target = new Vector2(4f, -2f);       // 故意斜向：竖直方向有问题才看得出来
            BallData data = ThrowFrom(origin, target, BallType.Earth);

            float previousHeight = -1f;
            float peakHeight = 0f;

            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;

                // 阴影位置：贴地逻辑位置。它的 y 不含任何高度，x 直接来自 SampleGround。
                Vector2 ground = data.SampleGround(t);

                float arc = data.MaxHeight * BallData.SampleHeight01(t);
                float ballHeight = arc + ThrowConstants.THROW_ORIGIN_HEIGHT;   // 球相对地面的高度

                // 弧高非负 ⇒ 球不会比阴影低（除了那一个固定的抬高量）。
                Assert.GreaterOrEqual(arc, -HeightTolerance, $"t={t}：弧高不该为负");

                // 球永远不低于出手抬高量：否则球会沉进地面，看起来像阴影飘在球上面。
                Assert.GreaterOrEqual(ballHeight, ThrowConstants.THROW_ORIGIN_HEIGHT - HeightTolerance,
                    $"t={t}：球的高度不该低于出手抬高量");

                // 防守"阴影跟着抛物线起伏"：前半程球的高度必须单调不减（抛物线在 0..0.5 上单调增）。
                if (i > 0 && t <= 0.5f)
                {
                    Assert.GreaterOrEqual(ballHeight, previousHeight - HeightTolerance,
                        $"t={t}：前半程球相对地面的高度应当单调不减");
                }

                previousHeight = ballHeight;
                peakHeight = Mathf.Max(peakHeight, ballHeight);

                // 落地的瞬间：弧高归零，球正好在出手抬高量上（与阴影的差只有一个固定值）。
                if (i == 100)
                {
                    Assert.AreEqual(ThrowConstants.THROW_ORIGIN_HEIGHT, ballHeight, HeightTolerance,
                        "落地瞬间球相对地面的高度必须恰好等于出手抬高量");
                }
            }

            // 顶点恰好是"出手抬高量 + MAX_HEIGHT"。
            Assert.AreEqual(ThrowConstants.THROW_ORIGIN_HEIGHT + ThrowConstants.MAX_HEIGHT, peakHeight, HeightTolerance,
                "球相对地面的最高点应当是 THROW_ORIGIN_HEIGHT + MAX_HEIGHT");

            // 出手抬高量必须为正，否则球从玩家身体里钻出来。
            Assert.Greater(ThrowConstants.THROW_ORIGIN_HEIGHT, 0f, "出手抬高量必须为正");

            // 贴地微偏移必须非正：为正会把阴影抬离地面。
            Assert.LessOrEqual(ThrowConstants.GROUND_VISUAL_OFFSET, 0f, "贴地偏移必须非正，否则阴影离地");

            // 指示器的形状参数必须在合法区间，否则贴图会画空或上下翻转。
            Assert.IsTrue(ThrowConstants.AIM_VERTICAL_SQUASH > 0f && ThrowConstants.AIM_VERTICAL_SQUASH <= 1f,
                "竖直压扁比例必须在 (0, 1]");
            Assert.IsTrue(ThrowConstants.AIM_PERSPECTIVE_TAPER >= 0f && ThrowConstants.AIM_PERSPECTIVE_TAPER < 1f,
                "上沿收窄比例必须在 [0, 1)，否则上沿会翻面");
            Assert.IsTrue(ThrowConstants.AIM_RING_THICKNESS > 0f && ThrowConstants.AIM_RING_THICKNESS < 1f,
                "环的厚度必须在 (0, 1)，否则环会整片实心或整个消失");
        }

        // ================================================================
        // W11 · 贴地环不得有断点
        // ================================================================

        /// <summary>
        /// 复刻 <c>PrimitiveSprites.BuildGroundRing</c> 的逐像素判定，把贴图形状在内存里重画一遍。
        /// </summary>
        /// <param name="size">贴图边长（像素）。</param>
        /// <param name="verticalSquash">竖直压扁比例。</param>
        /// <param name="taper">上半弧收窄比例。</param>
        /// <param name="thickness">环厚占半径比例。</param>
        /// <returns><c>[y, x]</c> 的实心表；y 与贴图坐标同向（0 在下）。</returns>
        private static bool[,] RasterizeRing(int size, float verticalSquash, float taper, float thickness, bool solid = false)
        {
            var px = new bool[size, size];

            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float rx = size * 0.5f;

            float vBase = rx * verticalSquash;
            float vTop = vBase * (1f - taper);

            // solid 时内圈缩到 0（不是"厚度取 1 再夹"），否则中心会留一个 10% 的洞。
            float innerScale = solid ? 0f : (1f - thickness);
            float ix = rx * innerScale;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;

                    float v = dy >= 0f ? vTop : vBase;
                    float iy = v * innerScale;

                    float outer = (dx * dx) / (rx * rx) + (dy * dy) / (v * v);
                    bool inside = outer <= 1f;

                    if (inside && !solid)
                    {
                        float inner = (dx * dx) / (ix * ix) + (dy * dy) / (iy * iy);
                        inside = inner >= 1f;
                    }

                    px[y, x] = inside;
                }
            }

            return px;
        }

        /// <summary>
        /// 贴地形状（环与实心盘）必须封闭：不得有空洞、不得左右断开。
        /// </summary>
        /// <remarks>
        /// <b>这一条是为两个真实缺陷写的。</b>
        /// <list type="number">
        /// <item>旧画法用"竖直半径随 x 线性变化"逐列算上下边界，那不是椭圆 ——
        /// 左右最宽处附近整行掉到 0，环断成上下两截（实测逐行宽度 <c>… 20 10 4 0 0 2 6 16 …</c>）。</item>
        /// <item>阴影用实心盘时，若靠"环厚取 1 再被夹到 0.9"来填实，内圈会留下 10% 半径的洞 ——
        /// 画出来是<b>甜甜圈</b>。所以 solid 时内圈必须直接缩到 0，本测试用"中心格必须为实"钉住。</item>
        /// </list>
        /// <para>判据取"每一行都有像素"（横向连续）+ "每一列在其上下界内连续"（纵向连续）。
        /// 纵向那条正是洞的杀手：中心有洞时，穿过洞的那几列会在洞里断掉。</para>
        /// </remarks>
        [TestCase(1.00f, 0.00f, false)]
        [TestCase(0.66f, 0.00f, false)]
        [TestCase(0.55f, 0.12f, false)]
        [TestCase(0.30f, 0.20f, false)]
        [TestCase(1.00f, 0.00f, true)]
        [TestCase(0.55f, 0.15f, true)]
        [TestCase(0.30f, 0.20f, true)]
        public void W11_贴地形状不得有断点或空洞(float verticalSquash, float taper, bool solid)
        {
            const int size = 64;
            bool[,] px = RasterizeRing(size, verticalSquash, taper, ThrowConstants.AIM_RING_THICKNESS, solid);

            string label = $"squash={verticalSquash} taper={taper} solid={solid}";

            // 先确认它真是"环"或"盘"而不是空图，否则下面的连续性判据会被平凡满足。
            int filled = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (px[y, x]) filled++;

            Assert.Greater(filled, 0, $"{label}：一个像素都没画");

            // 环不能是实心块（那说明内圈没生效）。
            // 实心盘**不设"至少填了多少"的下限**：压得越扁面积越小是几何必然，
            // 拿面积当判据会在最扁那组误报（试过）。实心盘的正确性由下面的"中心格为实 + 内部无空洞"保证。
            if (!solid) Assert.Less(filled, size * size / 2, $"{label}：画成了实心块，不是环");

            int bottom = -1;
            int top = -1;
            for (int y = 0; y < size && bottom < 0; y++)
                for (int x = 0; x < size; x++) if (px[y, x]) { bottom = y; break; }
            for (int y = size - 1; y >= 0 && top < 0; y--)
                for (int x = 0; x < size; x++) if (px[y, x]) { top = y; break; }

            Assert.GreaterOrEqual(bottom, 0, $"{label}：找不到底边");
            Assert.Greater(top, bottom, $"{label}：顶边必须高于底边");

            // 横向连续：从底到顶每一行都得有像素。断点那一行恰好一个都没有。
            for (int y = bottom; y <= top; y++)
            {
                Assert.Greater(CountRow(px, size, y), 0,
                    $"{label}：第 {y} 行（底边起第 {y - bottom} 行）是空的 —— " +
                    "形状在这里断开，左右两侧会看起来断成两截");
            }

            // 纵向连续（**只对实心盘**）：每一列在它自己的上下界之内都不得断。
            // 环的中间本来就是个洞，对环做这个断言会把正确的形状判成错的 —— 所以条件写在明处。
            if (solid)
            {
                for (int x = 0; x < size; x++)
                {
                    int colBottom = -1;
                    int colTop = -1;

                    for (int y = bottom; y <= top; y++)
                    {
                        if (!px[y, x]) continue;
                        if (colBottom < 0) colBottom = y;
                        colTop = y;
                    }

                    if (colBottom < 0) continue;   // 形状左右两端之外的列，本来就没有像素

                    for (int y = colBottom; y <= colTop; y++)
                    {
                        Assert.IsTrue(px[y, x],
                            $"{label}：第 {x} 列在第 {y} 行处是空的 —— 实心盘内部有空洞（画成了甜甜圈）");
                    }
                }

                // 中心格必须是实的：这是"甜甜圈"缺陷最直接的判据。
                Assert.IsTrue(px[size / 2, size / 2],
                    $"{label}：中心格是空的 —— 实心盘画成了甜甜圈（内圈没有真的缩到 0）");
            }

            // 左右对称：形状不该偏向一侧。
            for (int y = bottom; y <= top; y++)
            {
                int left = -1;
                int right = -1;
                for (int x = 0; x < size; x++) if (px[y, x]) { if (left < 0) left = x; right = x; }

                float mid = (left + right) * 0.5f;
                Assert.AreEqual(size * 0.5f - 0.5f, mid, 1f,
                    $"{label}：第 {y} 行的中点偏离了中心，形状歪向一侧");
            }

            // taper > 0 时，上下两半必须真的不一样：上半弧更平。
            if (taper <= 0f) return;

            // 度量取"上半的像素数 vs 下半的像素数"，不取某一行的宽度。
            // 理由：形状面积正比于该半弧的竖直半径，所以"更平"必然表现为上半像素更少；
            // 而"最顶行 vs 最底行"的宽度差只正比于 √(厚度)、约 4%，会被像素取整与逐行离散化淹没
            // —— 那条判据试过，不可靠（会误报）。
            int upperPixels = 0;
            int lowerPixels = 0;

            for (int y = bottom; y <= top; y++)
            {
                int row = CountRow(px, size, y);

                if (y >= size / 2) upperPixels += row;
                else lowerPixels += row;
            }

            Assert.Greater(upperPixels, 0, $"{label}：上半弧一个像素都没有");
            Assert.Greater(lowerPixels, 0, $"{label}：下半弧一个像素都没有");
            Assert.Less(upperPixels, lowerPixels,
                $"{label}：上半弧应当比下半弧更平（视平线不在中心）—— " +
                $"上半像素应少于下半，实测上半 {upperPixels} px、下半 {lowerPixels} px");
        }

        private static int CountRow(bool[,] px, int size, int y)
        {
            int c = 0;
            for (int x = 0; x < size; x++) if (px[y, x]) c++;
            return c;
        }

        private static void AssertColorInRange(Color color, string label)
        {
            Assert.IsTrue(color.r >= 0f && color.r <= 1f, $"{label} 的 r 分量越界：{color.r}");
            Assert.IsTrue(color.g >= 0f && color.g <= 1f, $"{label} 的 g 分量越界：{color.g}");
            Assert.IsTrue(color.b >= 0f && color.b <= 1f, $"{label} 的 b 分量越界：{color.b}");
            Assert.IsTrue(color.a >= 0f && color.a <= 1f, $"{label} 的 a 分量越界：{color.a}");
        }
    }
}
