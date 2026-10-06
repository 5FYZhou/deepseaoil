using UnityEngine;

namespace DeepseaOil.Presentation.Effects
{
    /// <summary>
    /// 一次特效播放的参数。纯数据，不含任何逻辑。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么用属性而不是裸字段</b>：<c>default(EffectContext)</c> 与
    /// <c>new EffectContext { Intensity = 0.5f }</c>（只写一部分字段）都是真实会出现的写法，
    /// 裸字段下它们会把 <c>Scale</c> / <c>Direction</c> 留成 0 —— 于是特效被缩到 1% 或朝向退化。
    /// 属性在读取处做归一化，所有构造路径都安全，而对象初始化器语法不变。</para>
    /// <list type="bullet">
    /// <item><see cref="Direction"/>：零向量读取时归一为 <c>Vector2.up</c>，否则返回单位向量。</item>
    /// <item><see cref="Scale"/>：<c>&lt;= 0</c> 读取时为 1。</item>
    /// <item><see cref="Intensity"/>：读取时 <c>Clamp01</c>。<b>未显式赋值即为 0</b>（0~1 语义下的最小值）——
    /// 想表达「满强度」请用 <see cref="At(Vector2)"/> 或显式写 <c>Intensity = 1f</c>。</item>
    /// <item><see cref="Tint"/>：alpha 为 0 读取时为白色。</item>
    /// <item><see cref="Radius"/>：<c>&lt;= 0</c> 读取时为 1。</item>
    /// <item><see cref="Duration"/>：<b>不归一化</b>，<c>&lt;= 0</c> 由驱动取自己的默认时长。</item>
    /// </list>
    /// <para><b>Intensity 的语义由 Driver 解释</b>，<c>EffectModule</c> 只传递不解释：
    /// <c>ScreenShake</c> 用它取最大值合并、<c>HitSpark</c> 用它缩放粒子量与大小、<c>MudSplash</c> 用它控制飞溅量。
    /// 参考实现见 <c>ParticleDriver.ApplyIntensity</c>。</para>
    /// </remarks>
    public struct EffectContext
    {
        /// <summary>世界坐标位置（特效根在原点，所以它就是世界坐标）。</summary>
        public Vector2 Position;

        private Vector2 _direction;

        /// <summary>方向。零向量读取时为 <c>Vector2.up</c>，否则为单位向量。</summary>
        public Vector2 Direction
        {
            get => _direction.sqrMagnitude > 0f ? _direction.normalized : Vector2.up;
            set => _direction = value;
        }

        private float _scale;

        /// <summary>缩放。<c>&lt;= 0</c> 读取时为 1。</summary>
        public float Scale
        {
            get => _scale > 0f ? _scale : 1f;
            set => _scale = value;
        }

        private float _intensity;

        /// <summary>强度 0~1，由 Driver 解释。</summary>
        public float Intensity
        {
            get => Mathf.Clamp01(_intensity);
            set => _intensity = value;
        }

        private Color _tint;

        /// <summary>
        /// 着色。<b>未设置（alpha 为 0）时读取为白色</b>，与 <see cref="Scale"/> / <see cref="Direction"/>
        /// 同一条"读取处归一化"的纪律：<c>default(EffectContext)</c> 与只写一部分字段的对象初始化器
        /// 都不会把颜色留成透明（透明 = 什么也看不见，且不报错）。
        /// </summary>
        /// <remarks>
        /// 只有把它当参数的驱动才读它（<c>EnemyShatter</c> / <c>TileHighlight</c>）；
        /// 粒子驱动刻意忽略它 —— 颜色在预制体里，两处都能定色会让人分不清哪一处生效。
        /// </remarks>
        public Color Tint
        {
            get => _tint.a > 0f ? _tint : Color.white;
            set => _tint = value;
        }

        private float _radius;

        /// <summary>
        /// 半径（世界单位）。<c>&lt;= 0</c> 读取时为 1。
        /// </summary>
        /// <remarks>
        /// 给"半径即语义"的驱动用：落地环画的圈就是某件事的生效范围，半径必须是参数而不是常量，
        /// 否则改一次生效半径就得回来改特效。
        /// </remarks>
        public float Radius
        {
            get => _radius > 0f ? _radius : 1f;
            set => _radius = value;
        }

        private float _duration;

        /// <summary>
        /// 持续时长（秒）。<b>不归一化</b>：<c>&lt;= 0</c> 表示"用驱动自己的默认时长"。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="Radius"/> 的处理不同是刻意的：半径没有"合理默认"，而时长有
        /// （每种特效都有一个作者写死的时长），把 0 归一成一个通用值会掩盖"调用方没传"这件事。
        /// </remarks>
        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        /// <summary>跟随目标，可空。非空时每帧把特效挪到它身上；目标被销毁则自动停止。</summary>
        public Transform Follow;

        /// <summary>是否要求跟随。</summary>
        public bool FollowRequested => Follow != null;

        /// <summary>在某个世界坐标播一次（满强度）。最常用的入口。</summary>
        public static EffectContext At(Vector2 position)
            => new EffectContext
            {
                Position = position,
                Direction = Vector2.up,
                Scale = 1f,
                Intensity = 1f,
                Follow = null,
            };

        /// <summary>在某个世界坐标、带方向播一次（满强度）。</summary>
        public static EffectContext At(Vector2 position, Vector2 direction)
            => new EffectContext
            {
                Position = position,
                Direction = direction,
                Scale = 1f,
                Intensity = 1f,
                Follow = null,
            };

        /// <summary>跟随一个 Transform 播一次（满强度）。目标为 null 时退化为原点，不抛异常。</summary>
        public static EffectContext OnTarget(Transform target)
            => new EffectContext
            {
                Position = target != null ? (Vector2)target.position : Vector2.zero,
                Direction = Vector2.up,
                Scale = 1f,
                Intensity = 1f,
                Follow = target,
            };

        /// <summary>在原点、满强度的默认上下文。</summary>
        public static EffectContext Default => At(Vector2.zero);

        public override string ToString()
            => $"EffectContext(pos=({Position.x:F2}, {Position.y:F2}) dir=({Direction.x:F2}, {Direction.y:F2}) " +
               $"scale={Scale:F2} intensity={Intensity:F2} tint={Tint} radius={Radius:F2} duration={Duration:F2} " +
               $"follow={(Follow != null ? Follow.name : "null")})";
    }
}
