using UnityEngine;
using cfg.dso;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>元素合成：两份元素（球 ⊕ 地形）折成一份，纯函数</summary>
    /// <remarks>温度相加夹[-6, 6]、湿度相加夹[0, 6]、导电取较大、标签按位或；Type 不参与判定，判据只有温/湿/导电/标签</remarks>
    public static class ElementCombiner
    {
        /// <summary>合成两份元素</summary>
        public static ElementValue Combine(in ElementValue a, in ElementValue b)
        {
            int temperature = Mathf.Clamp(
                a.Temperature + b.Temperature,
                ElementValue.MinTemperature,
                ElementValue.MaxTemperature);

            int wet = Mathf.Clamp(
                a.Wet + b.Wet,
                ElementValue.MinWet,
                ElementValue.MaxWet);

            int conductivity = Mathf.Max(a.Conductivity, b.Conductivity);

            ElementTag tags = a.Tags | b.Tags;

            return new ElementValue(cfg.dso.ElementType.Environment, tags, temperature, wet, conductivity);
        }
    }
}
