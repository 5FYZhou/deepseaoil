using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>元素合成：把两份元素（球 ⊕ 地形）折成一份。纯函数、无状态。</summary>
    /// <remarks>
    /// 口径（照收上游算法，§7 关卡 1 的 C：算法是资产）：
    /// 温度相加后夹 <c>[-6, 6]</c>、湿度相加后夹 <c>[0, 6]</c>、导电取两者较大、标签按位或。
    /// <para><b><c>Type</c> 不参与判定</b>：结果里给一个 <c>Environment</c> 占位（上游写死 <c>Earth</c>，那更没道理 —— 合成结果既不必然是土）。判定只看温 / 湿 / 导电 / 标签（与 D13"先保持，不动"一致：<c>Type</c> 现在没有任何读者）。</para>
    /// </remarks>
    public static class ElementCombiner
    {
        /// <summary>合成两份元素。</summary>
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

            return new ElementValue(cfg.demo.ElementType.Environment, tags, temperature, wet, conductivity);
        }
    }
}
