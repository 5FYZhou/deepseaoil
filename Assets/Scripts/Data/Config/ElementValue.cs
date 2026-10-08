using cfg.dso;

namespace DeepseaOil.Data
{
    /// <summary>一份元素：温度/湿度/导电/标签，球与格地形元素同类型，合成即两份相加</summary>
    /// <remarks>是值不是身份，判定只看四个数，Type 不参与判定（合成写死 Earth）。范围是约定非校验：温度 -6..6、湿度 0..6、导电 0..2（-1 只在规则里表示不检查），填超界不报错、只是永不命中；夹取由 ElementCombiner 做，本类型不做</remarks>
    public readonly struct ElementValue
    {
        public readonly ElementType Type;

        /// <summary>标签位（[Flags]），合成按位或，规则用必须含/必须不含两条判据</summary>
        public readonly ElementTag Tags;

        /// <summary>温度，约定 -6..6</summary>
        public readonly int Temperature;

        /// <summary>湿度，约定 0..6</summary>
        public readonly int Wet;

        /// <summary>导电，约定 0 无/1 中/2 强</summary>
        public readonly int Conductivity;

        public ElementValue(ElementType type, ElementTag tags, int temperature, int wet, int conductivity)
        {
            Type = type;
            Tags = tags;
            Temperature = temperature;
            Wet = wet;
            Conductivity = conductivity;
        }

        /// <summary>温度下界，合成夹取用；规则表里 -6 的来源</summary>
        public const int MinTemperature = -6;

        public const int MaxTemperature = 6;

        /// <summary>湿度下界，合成夹取用</summary>
        public const int MinWet = 0;

        public const int MaxWet = 6;

        public bool IsEmpty => Tags == 0 && Temperature == 0 && Wet == 0 && Conductivity == 0;

        public override string ToString()
        {
            return $"({Type} temp={Temperature} wet={Wet} cond={Conductivity} tags={Tags})";
        }
    }
}
