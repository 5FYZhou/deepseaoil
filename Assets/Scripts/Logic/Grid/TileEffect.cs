namespace DeepseaOil.Logic.Grid
{
    /// <summary>地块效果的种类。<b>取值与 <c>cfg.demo.TileEffectType</c> 的表号一一对应</b>（所以能直接按效果号查表取档位）。</summary>
    /// <remarks>
    /// <b>只有 8 个成员有实现</b>：<see cref="Slow"/> / <see cref="Slide"/> / <see cref="KnockBack"/> / <see cref="InstantDamage"/> / <see cref="DamageOverTime"/> / <see cref="InheritElement"/> / <see cref="ClearPlants"/> 与 <see cref="None"/>（= 本行无效果）。
    /// <c>Skid</c>(2) / <c>Block</c>(5) / <c>Fixed</c>(7) 按 §6"本轮不做"保留枚举位与表行，不写实现 —— 表里也没引用它们。
    /// </remarks>
    public enum TileEffectKind
    {
        /// <summary>无效果（表里 <c>id = 0</c>；也是"这份数据没有"的默认值）。</summary>
        None = 0,

        /// <summary>减速：<c>scale = 速度倍率</c>（<c>0.5</c> = 半速），<c>seconds</c> = 这次修饰的续命时长。</summary>
        Slow = 1,

        /// <summary>滑行：敌人沿当前朝向滑 <c>cells</c> 格、<c>seconds</c> 秒。<b>本轮未实现</b>（需要"让敌人滑行"的能力接口，见 §10 的接口上限）。</summary>
        Slide = 3,

        /// <summary>击退：<c>cells</c> = 击退距离（格）。冲量由执行者按格边长折算。</summary>
        KnockBack = 4,

        /// <summary>麻痹：<c>seconds</c> = 不能行动的时长。<b>本轮只到"提交给目标"这一层</b>，进移动门禁要等 Actor 侧的计时器接线。</summary>
        Numbness = 6,

        /// <summary>瞬时伤害：<c>amount</c> = 伤害值。</summary>
        InstantDamage = 8,

        /// <summary>持续伤害：每 <c>interval</c> 秒扣 <c>perTick</c>，累计 <c>seconds</c>（<c>0</c> = 持续到状态结束）。</summary>
        DamageOverTime = 9,

        /// <summary>温湿度继承：把该格元素按三个比率放大（<c>1</c> = 原样）。</summary>
        InheritElement = 10,

        /// <summary>清除植物：清掉该格元素里的"含植物"标签位（<c>radius</c> = 表里声明的十字范围，<b>当前只作用于本格</b>）。</summary>
        ClearPlants = 11,
    }

    /// <summary>
    /// 一次<b>已定值</b>的格子效果：<see cref="Kind"/> ＋ 该种类用到的槽位。
    /// </summary>
    /// <remarks>
    /// <b>不认识的"档位"在这里已经不存在了</b>：多档的选择发生在数据层（<c>TileEffectSpec.GetEffect(pos)</c>），Logic 层只看到定值 —— 于是本类型与 <see cref="ITileResolver.Apply"/> 的签名不会随效果数量增长。
    /// <b>只能经工厂构造</b>：三个槽位按 <see cref="Kind"/> 解释，工厂方法保证"哪个种类用哪几个槽"不会对不上；读取时也必须先看 <see cref="Kind"/> 再取对应属性。
    /// </remarks>
    public readonly struct TileEffect
    {
        public readonly TileEffectKind Kind;

        /// <summary>槽位 1：伤害值 / 滑行格数 / 击退格数 / 每次伤害 / 每秒伤害 / 清除范围。</summary>
        private readonly float _a;

        /// <summary>槽位 2：持续时间（秒）/ 伤害间隔（秒）/ 湿度继承比率 / 每帧伤害。</summary>
        private readonly float _b;

        /// <summary>槽位 3：温度继承比率 / 导电继承比率 / 累计时长。</summary>
        private readonly float _c;

        private TileEffect(TileEffectKind kind, float a, float b, float c)
        {
            Kind = kind;
            _a = a;
            _b = b;
            _c = c;
        }

        /// <summary>速度倍率（<see cref="TileEffectKind.Slow"/>）；<c>1</c> = 不减速。</summary>
        public float Scale => _a;

        /// <summary>持续时间（秒）。</summary>
        public float Seconds => _b;

        /// <summary>伤害值（<see cref="TileEffectKind.InstantDamage"/>）。</summary>
        public float Amount => _a;

        /// <summary>每次伤害（<see cref="TileEffectKind.DamageOverTime"/>）。</summary>
        public float PerTick => _a;

        /// <summary>伤害间隔（秒，<see cref="TileEffectKind.DamageOverTime"/>）；<c>0</c> = 每帧。</summary>
        public float Interval => _b;

        /// <summary>累计时长（秒，<see cref="TileEffectKind.DamageOverTime"/>）；<c>0</c> = 持续到状态结束。</summary>
        public float Duration => _c;

        /// <summary>滑行 / 击退的距离（格）。</summary>
        public float Cells => _a;

        /// <summary>清除范围（格，<see cref="TileEffectKind.ClearPlants"/>）。</summary>
        public int Radius => (int)_a;

        /// <summary>温度继承比率（<see cref="TileEffectKind.InheritElement"/>）。</summary>
        public float TemperatureRatio => _a;

        /// <summary>湿度继承比率（<see cref="TileEffectKind.InheritElement"/>）。</summary>
        public float WetRatio => _b;

        /// <summary>导电继承比率（<see cref="TileEffectKind.InheritElement"/>）。</summary>
        public float ConductivityRatio => _c;

        /// <summary>减速修饰（<paramref name="scale"/> = 速度倍率，<paramref name="seconds"/> = 续命时长）。</summary>
        public static TileEffect Slow(float scale, float seconds)
            => new TileEffect(TileEffectKind.Slow, scale, seconds, 0f);

        /// <summary>瞬时伤害（<paramref name="amount"/> = 伤害值）。</summary>
        public static TileEffect InstantDamage(float amount)
            => new TileEffect(TileEffectKind.InstantDamage, amount, 0f, 0f);

        /// <summary>击退（<paramref name="cells"/> = 距离，单位是格）。</summary>
        public static TileEffect KnockBack(float cells)
            => new TileEffect(TileEffectKind.KnockBack, cells, 0f, 0f);

        /// <summary>滑行（<paramref name="cells"/> 格、<paramref name="seconds"/> 秒）。</summary>
        public static TileEffect Slide(int cells, float seconds)
            => new TileEffect(TileEffectKind.Slide, cells, seconds, 0f);

        /// <summary>麻痹（<paramref name="seconds"/> 秒内不能行动）。</summary>
        public static TileEffect Numbness(float seconds)
            => new TileEffect(TileEffectKind.Numbness, seconds, 0f, 0f);

        /// <summary>持续伤害（每 <paramref name="interval"/> 秒扣 <paramref name="perTick"/>，累计 <paramref name="seconds"/> 秒）。</summary>
        public static TileEffect DamageOverTime(float perTick, float interval, float seconds)
            => new TileEffect(TileEffectKind.DamageOverTime, perTick, interval, seconds);

        /// <summary>温湿度继承（三个比率，<c>1</c> = 原样）。</summary>
        public static TileEffect InheritElement(float tempRatio, float wetRatio, float condRatio)
            => new TileEffect(TileEffectKind.InheritElement, tempRatio, wetRatio, condRatio);

        /// <summary>清除植物（<paramref name="radius"/> = 表里声明的范围，单位格）。</summary>
        public static TileEffect ClearPlants(int radius)
            => new TileEffect(TileEffectKind.ClearPlants, radius, 0f, 0f);

        public override string ToString()
        {
            return Kind switch
            {
                TileEffectKind.Slow => $"Slow(scale={Scale}, {Seconds}s)",
                TileEffectKind.InstantDamage => $"InstantDamage({Amount})",
                TileEffectKind.KnockBack => $"KnockBack({Cells}格)",
                TileEffectKind.Slide => $"Slide({Cells}格, {Seconds}s)",
                TileEffectKind.Numbness => $"Numbness({Seconds}s)",
                TileEffectKind.DamageOverTime => $"DoT({PerTick}/{Interval}s, {Duration}s)",
                TileEffectKind.InheritElement => $"InheritElement(temp={TemperatureRatio}, wet={WetRatio}, cond={ConductivityRatio})",
                TileEffectKind.ClearPlants => $"ClearPlants(r={Radius})",
                _ => "None",
            };
        }
    }
}
