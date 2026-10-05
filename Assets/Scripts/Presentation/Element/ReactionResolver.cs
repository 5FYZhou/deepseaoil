using Assets.Scripts.Data;
using cfg.demo;
using DeepseaOil.Data;
using System.Collections.Generic;

namespace DeepseaOil.Presentation.Element
{

    public class ReactionResult
    {
        public int Id;
        public ElementSpec Element;
        public List<IEffect> Effects;
    }

    public class ReactionResolver
    {
        private IReadOnlyList<ElementRuleSpec> _rules;

        public ReactionResolver(IReadOnlyList<ElementRuleSpec> r)
        {
            _rules = r;
        }

        public ReactionResult MatchRule(ElementSpec element)
        {
            foreach (var rule in _rules)
            {
                if (rule.Match(element))
                {
                    return CreateResult(rule.ResultTileType);
                }
            }

            return CreateFallback();
        }

        private ReactionResult CreateResult(TileType resultId)
        {
            return new();
        }

        private ReactionResult CreateFallback()
        {
            return new();
        }
    }
}