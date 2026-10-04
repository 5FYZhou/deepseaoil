using System.Collections.Generic;

namespace DeepseaOil.Presentation.Element
{
    public class AffectRule
    {
        public int Priority;

        public ElementTag RequireTags;
        public ElementTag ExcludeTags;

        public int TemperatureMin;
        public int TemperatureMax;

        public int HumidityMin;
        public int HumidityMax;

        public Conductivity ConductivityMin;

        public int ResultId;

        public bool Match(Element element)
        {
            if ((element.Tags & RequireTags) != RequireTags)
                return false;

            if ((element.Tags & ExcludeTags) != 0)
                return false;

            if (element.Temperature < TemperatureMin)
                return false;

            if (element.Temperature > TemperatureMax)
                return false;

            if (element.Humidity < HumidityMin)
                return false;

            if (element.Humidity > HumidityMax)
                return false;

            if (element.Conductivity < ConductivityMin)
                return false;

            return true;
        }
    }

    public class ReactionResult
    {
        public int Id;
        public Element Element;
        public List<IEffect> Effects;
    }

    public class ReactionResolver
    {
        private readonly List<AffectRule> _rules;

        public void InitRules()
        {
            // 加载配置
            // 创建rules
            AffectRule a = new();
            a.Priority = 1;
            a.RequireTags = ElementTag.None;
            a.ExcludeTags = ElementTag.Sand & ElementTag.Soil & ElementTag.Plant;
            a.TemperatureMin = 4;
            a.TemperatureMax = 6;
            a.HumidityMin = 2;
            a.HumidityMax = 6;
            a.ConductivityMin = Conductivity.None;
            a.ResultId = 4007;
            // 按优先级加入数组
            _rules.Add(a);
        }

        public ReactionResult MatchRule(Element element)
        {
            foreach (var rule in _rules)
            {
                if (rule.Match(element))
                {
                    return CreateResult(rule.ResultId);
                }
            }

            return CreateFallback();
        }

        private ReactionResult CreateResult(int resultId)
        {
            return new();
        }

        private ReactionResult CreateFallback()
        {
            return new();
        }
    }
}