using cfg.demo;
using DeepseaOil.Data;
using System;
using System.Collections.Generic;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 存储规则
    /// 给出反应结果
    /// </summary>
    public class ReactionResolver
    {
        private IReadOnlyList<ElementRuleSpec> _rules;

        public ReactionResolver(IReadOnlyList<ElementRuleSpec> r)
        {
            _rules = r;
        }

        /// <summary>
        /// 根据球和地形的元素总和，查询规则返回生成的地形类型和效果(OnEnter时消费
        /// 已包含兜底（最后一条规则）
        /// </summary>
        public bool MatchRule(ElementSpec element, out TileStateType tileStateType, out TileEffectInfoSpec effectInfoSpec)
        {
            foreach (var rule in _rules)
            {
                if (rule.Match(element))
                {
                    tileStateType = rule.ResultTileType;
                    effectInfoSpec = rule.EffectInfoSpec;
                    return true;
                }
            }
            var lastrule = _rules[_rules.Count - 1];
            tileStateType = lastrule.ResultTileType;
            effectInfoSpec = lastrule.EffectInfoSpec;
            return false;
        }
    }
}