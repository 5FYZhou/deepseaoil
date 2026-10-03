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
               $"scale={Scale:F2} intensity={Intensity:F2} follow={(Follow != null ? Follow.name : "null")})";
    }
}
