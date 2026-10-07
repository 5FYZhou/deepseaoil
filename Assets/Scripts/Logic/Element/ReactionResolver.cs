using System.Collections.Generic;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>规则匹配：在一份元素上找<b>第一条</b>命中的反应规则。纯函数、无状态（规则清单由调用方给）。</summary>
    /// <remarks>
    /// <b>顺序即优先级</b>：不为 <c>priority</c> 排序 —— 清单的顺序就是判定顺序，表的读取顺序由 <c>ConfigModule.GetElementRules()</c> 保证。
    /// <para><b>无命中时返回最后一条规则并给 <c>false</c></b>（照收上游语义）：真数据里最后一条是"兜底行"（结果 <c>None</c> ＋ 一点击退），
    /// 所以"什么都没匹配上"表现为"格子不变 ＋ 兜底行那条效果照常发生"。把它改成就地不动会让配表里那些兜底行永远失效。</para>
    /// <para>清单为空时返回 <c>false</c> 且不给结果 —— 调用方据此<b>什么都不做</b>（没有规则表就不该发生任何反应）。</para>
    /// <para><b>两个类型写全名</b>（<c>TileStateType</c> 与 <c>TileEffect</c>）：它们在 <c>cfg.demo</c> 与 <c>DeepseaOil.Logic.Grid</c> 里各有一份同名物，
    /// 而本层不该为了少打几个字去把 <c>cfg.demo</c> 整个引进来（那会让"元素层只认识枚举与已定值效果"这条边界糊掉）。</para>
    /// </remarks>
    public static class ReactionResolver
    {
        /// <summary>匹配一份元素。</summary>
        /// <param name="rules">规则清单（顺序即优先级）。</param>
        /// <param name="element">合成后的元素。</param>
        /// <param name="result">命中的结果状态；无命中时是最后一条规则的结果。</param>
        /// <param name="effects">命中的效果清单；无命中时是最后一条规则的清单。</param>
        /// <returns>是否<b>真的</b>有规则命中（<c>false</c> 表示走的是最后一条兜底行）。</returns>
        public static bool Match(
            IReadOnlyList<ElementRuleSpec> rules,
            in ElementValue element,
            out cfg.demo.TileStateType result,
            out IReadOnlyList<DeepseaOil.Logic.Grid.TileEffect> effects)
        {
            result = cfg.demo.TileStateType.None;
            effects = null;

            if (rules == null || rules.Count == 0) return false;

            for (int i = 0; i < rules.Count; i++)
            {
                if (!rules[i].Match(in element)) continue;

                result = rules[i].ResultTileType;
                effects = rules[i].Effects;

                return true;
            }

            // 兜底：上游就是这么写的（返回最后一条），并让调用方知道"这不是命中"。
            ElementRuleSpec last = rules[rules.Count - 1];

            result = last.ResultTileType;
            effects = last.Effects;

            return false;
        }
    }
}
