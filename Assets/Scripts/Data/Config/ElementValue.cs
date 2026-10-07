using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>一份<b>元素</b>：温度 / 湿度 / 导电 / 标签。球的元素与格子的地形元素用的是同一个类型 —— 合成就是"两份元素相加"。</summary>
    /// <remarks>
    /// <b>它是值而不是身份</b>：判定只看这四个数，<c>Type</c> 当前不参与任何判定（上游的合成结果把 <c>Type</c> 写死成 <c>Earth</c>，属占位；见 D13"先保持，不动"）。
    /// 取值范围是<b>约定</b>而不是校验：温度 <c>-6..6</c>、湿度 <c>0..6</c>、导电 <c>0..2</c>（<c>-1</c> 只在规则里表示"不检查"）。表里填超界的数不会报错，只会在规则匹配上表现为"永远不命中"。
    /// 合成（<c>ElementCombiner</c>）负责把结果夹回范围；本类型不做夹取 —— 它同时要承载"表里就是这么填的"这一事实。
    /// </remarks>
    public readonly struct ElementValue
    {
        /// <summary>类型。<b>当前不参与判定</b>（枚举里也没有火 / 冰：火是 <c>temp = 4</c>、冰是 <c>temp = -4</c>）。</summary>
        public readonly ElementType Type;

        /// <summary>标签位（<c>[Flags]</c>：含土 / 含沙 / 含植物）。合成按位或，规则用"必须含 / 必须不含"两条判据。</summary>
        public readonly ElementTag Tags;

        /// <summary>温度（约定 <c>-6..6</c>）。</summary>
        public readonly int Temperature;

        /// <summary>湿度（约定 <c>0..6</c>）。</summary>
        public readonly int Wet;

        /// <summary>导电（约定 <c>0</c> 无 / <c>1</c> 中 / <c>2</c> 强）。</summary>
        public readonly int Conductivity;

        public ElementValue(ElementType type, ElementTag tags, int temperature, int wet, int conductivity)
        {
            Type = type;
            Tags = tags;
            Temperature = temperature;
            Wet = wet;
            Conductivity = conductivity;
        }

        /// <summary>温度的取值范围（合成时夹取；也是规则表里那些 <c>-6 / 6</c> 的来源）。</summary>
        public const int MinTemperature = -6;

        public const int MaxTemperature = 6;

        /// <summary>湿度的取值范围（合成时夹取）。</summary>
        public const int MinWet = 0;

        public const int MaxWet = 6;

        /// <summary>是否是一份"什么都没有"的元素（四件全零），诊断 / 断言用。</summary>
        public bool IsEmpty => Tags == 0 && Temperature == 0 && Wet == 0 && Conductivity == 0;

        public override string ToString()
        {
            return $"({Type} temp={Temperature} wet={Wet} cond={Conductivity} tags={Tags})";
        }
    }
}
