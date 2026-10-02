using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 泥浆：水球落地后留在地上的一片减速区，<see cref="ThrowConstants.MUD_DURATION"/> 秒后消失。
    /// </summary>
    /// <remarks>
    /// <b>判定与形状都只看水平分量，竖直方向不判。</b>泥浆盘是"贴地的一个压扁的圆"，
    /// 它的竖直半径只有水平半径的 <see cref="ThrowConstants.AIM_VERTICAL_SQUASH"/> 倍。
    /// 如果按正圆判，玩家会看到"我明明在圈外、却被减速了" —— 判定范围比画出来的圈高将近一倍。
    /// <para><b>减速是"取样"而不是"进入/离开事件"：</b>没有触发器、没有层、没有 onEnter/onExit 状态。
    /// 敌人每帧问一次"我踩在泥里吗、多黏"，所以泥浆消失的那一刻敌人自然就恢复正常，
    /// 不需要任何人去通知它。代价是每帧一次查询，白模完全可接受。</para>
    /// <para>用 <c>Time.deltaTime</c>（受 timeScale 影响）推进寿命：暂停时它变 0，泥浆自然冻结，
    /// 恢复后从原进度继续 —— 与球用的是同一个理由。</para>
    /// </remarks>
    public sealed class MudPatch : MonoBehaviour
    {
        /// <summary>
        /// 泥浆底图烘制用的基准半径（世界单位）。
        /// </summary>
        /// <remarks>
        /// <b>1 米这个值本身就是"底图直径 = 2 米"</b>，于是 <see cref="PrimitiveSprites.ConfigureGround"/>
        /// 的换算系数恰好等于目标直径 —— 与本类自己算缩放的那行公式是同一个数。
        /// 固定成基准而不是"按实际半径烘"，是因为本类<b>每帧都要改缩放</b>来表现淡出；
        /// 按实际半径烘的话，那份缩放里会混进一个已经烘进贴图的量，改半径时外观会双重生效。
        /// </remarks>
        private const float BakedRadius = 1f;

        private float _radius;
        private float _verticalRadius;
        private float _alpha;
        private float _elapsed;
        private float _duration;
        private SpriteRenderer _renderer;

        /// <summary>泥浆中心的水平半径（世界单位）。</summary>
        public float Radius => _radius;

        /// <summary>
        /// 建出泥浆盘并开始计时。
        /// </summary>
        /// <param name="radiusMeters">中心处的水平半径（世界单位）。</param>
        /// <param name="duration">存在时长（秒）。非正数时退回 <see cref="ThrowConstants.MUD_DURATION"/>。</param>
        public void Initialize(float radiusMeters, float duration)
        {
            _radius = radiusMeters;

            // 竖直半径由形状常量推导，与画出来的贴地形状同一份来源：
            // 两处各自算一次的话，"看得见的圈"与"判定生效的圈"迟早会漂移。
            _verticalRadius = radiusMeters * ThrowConstants.AIM_VERTICAL_SQUASH;

            // 非正时长会让进度除法产出非数。上限不是随手加的：白模是临时物，宁可退回默认也不抛异常。
            _duration = duration > 0f ? duration : ThrowConstants.MUD_DURATION;

            _renderer = gameObject.AddComponent<SpriteRenderer>();

            Color color = ThrowConstants.MUD_COLOR;
            _alpha = color.a;

            // 实心盘：泥浆是一摊"东西"，不是一圈"线" —— 与阴影（也是实心盘）同一个视觉家族，
            // 与指示器/瞬闪（环）区分开。
            Sprite disc = PrimitiveSprites.GroundDiscOrRing(BakedRadius, solid: true);

            PrimitiveSprites.ConfigureGround(
                _renderer,
                disc,
                color,
                ThrowConstants.MUD_SORTING_ORDER,
                BakedRadius,
                radiusMeters * 2f
                );

            // 首帧就摆到起始尺寸：ConfigureGround 写进去的是完整尺寸，不覆盖会先闪一帧满圈再缩。
            Apply(1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            float remaining = Mathf.Clamp01(1f - _elapsed / _duration);

            // 后半程才开始变淡：前半程保持满不透明，玩家才来得及看清"这里有一片泥"。
            float fade = Mathf.Clamp01(remaining * 2f);

            Apply(fade);

            if (remaining <= 0f) Destroy(gameObject);
        }

        /// <summary>按淡出进度摆尺寸与透明度；<paramref name="fade"/> = 1 是刚落地。</summary>
        private void Apply(float fade)
        {
            // 略微收缩：只靠透明度变化，在浅色地面上几乎看不出它正在消失。
            float diameter = _radius * Mathf.Lerp(0.75f, 1f, fade) * 2f;

            transform.localScale = new Vector3(diameter, diameter, 1f);

            if (_renderer == null) return;

            Color c = _renderer.color;

            _renderer.color = new Color(c.r, c.g, c.b, _alpha * fade);
        }

        /// <summary>
        /// 点是否踩在这片泥里。<b>静态纯函数</b>。
        /// </summary>
        /// <param name="center">泥浆中心。</param>
        /// <param name="point">要判断的点（角色位置）。</param>
        /// <param name="radius">中心处的水平半径。</param>
        /// <param name="verticalRadius">竖直半径（贴地形状的压扁量）。</param>
        /// <returns>踩在里面为 <c>true</c>。</returns>
        /// <remarks>
        /// 用"逐分量椭圆判定"而不是 <c>Vector2.Distance</c>：后者是正圆，与画出来的贴地形状不符。
        /// 返回布尔而不是"重叠多少"：减速是一档常量（<see cref="ThrowConstants.MUD_SLOW_FACTOR"/>），
        /// 不做按距离渐变 —— 渐变会让"我到底会被减速多久"变得只能靠试。
        /// </remarks>
        public static bool Contains(Vector2 center, Vector2 point, float radius, float verticalRadius)
        {
            if (radius <= 0f) return false;

            // 竖直半径退化为 0 时（形状常量被改成 0），按"只判水平"处理：
            // 那样至少不会除零产出非数，也不会把整片泥判成无效。
            float vertical = verticalRadius > 0f ? verticalRadius : 1f;

            Vector2 delta = point - center;

            float normalized = delta.x * delta.x / (radius * radius)
                + delta.y * delta.y / (vertical * vertical);

            return normalized <= 1f;
        }

        /// <summary>实例版判定，自己取半径与竖直半径。</summary>
        public bool Contains(Vector2 point)
        {
            return Contains(transform.position, point, _radius, _verticalRadius);
        }

        /// <summary>
        /// 从一堆泥浆里找出对某点<b>最强</b>的一档减速。<b>静态纯函数</b>。
        /// </summary>
        /// <param name="point">角色位置。</param>
        /// <param name="patches">当前场上的全部泥浆。<b>可以是 <c>null</c></b>（表示场上没有泥浆）。</param>
        /// <returns>减速系数：没踩到任何泥浆时是 <c>1</c>，踩到则是 <see cref="ThrowConstants.MUD_SLOW_FACTOR"/>。</returns>
        /// <remarks>
        /// <b>取最强，不做乘算。</b>两片泥浆各自乘一次会得到 0.2，三片是 0.09 ——
        /// 敌人几乎定在原地，看起来像"泥浆把它粘住了"，而这是坏掉的手感而不是设计。
        /// 更糟的是它<b>不报错</b>：只会表现为"叠几片泥敌人就不动了"，很难倒推到乘算上。
        /// <para>返回系数而不是布尔值：将来泥浆分等级（大水球更黏）时只需换一个数。</para>
        /// </remarks>
        public static float StrongestSlow(Vector2 point, IReadOnlyList<MudPatch> patches)
        {
            if (patches == null) return 1f;

            float slowest = 1f;

            for (int i = 0; i < patches.Count; i++)
            {
                MudPatch patch = patches[i];

                // 已销毁的对象在同帧内还留在列表里（Destroy 是延迟的），Unity 的 null 判定会挡住它。
                if (patch == null) continue;
                if (!patch.Contains(point)) continue;

                if (ThrowConstants.MUD_SLOW_FACTOR < slowest) slowest = ThrowConstants.MUD_SLOW_FACTOR;
            }

            return slowest;
        }
    }
}
