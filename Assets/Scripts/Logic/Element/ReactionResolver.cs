using System.Collections.Generic;
using cfg.demo;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>规则匹配：在一份元素上找第一条命中的规则，纯函数（清单由调用方给）</summary>
    /// <remarks>顺序即优先级：不按 priority 排序，清单顺序即判定顺序（读取顺序由 ConfigModule.GetElementRules() 保证）；无命中时返回最后一条兜底行并给 false，效果照常发生；清单为空时返回 false 且不给结果</remarks>
    public static class ReactionResolver
    {
        /// <summary>匹配一份元素</summary>
        /// <remarks>result/effects 无命中时取最后一条；false 表示走的是兜底行</remarks>
        public static bool Match(
            IReadOnlyList<ElementRuleSpec> rules,
            in ElementValue element,
            out TileStateType result,
            out IReadOnlyList<TileEffectValue> effects)
        {
            result = TileStateType.None;
            effects = null;

            if (rules == null || rules.Count == 0) return false;

            for (int i = 0; i < rules.Count; i++)
            {
                if (!rules[i].Match(in element)) continue;

                result = rules[i].ResultTileType;
                effects = rules[i].Effects;

                return true;
            }

            // 兜底：返回最后一条，并告知调用方未命中。
            ElementRuleSpec last = rules[rules.Count - 1];

            result = last.ResultTileType;
            effects = last.Effects;

            return false;
        }
    }
}
