using System.Collections.Generic;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>一条元素反应规则，持有 element_rule 表行，构造期解析成已定值 TileEffectValue</summary>
    /// <remarks>匹配按列表先后，首个命中者胜出；priority 是主键，只用于诊断排序。效果清单构造期解析，帧内零查表；越界档位由 TileEffectSpec 报Warning 夹到第1档</remarks>
    public sealed class ElementRuleSpec
    {
        /// <summary>取某效果第 pos 档的解析器，由 ConfigModule 提供；null=效果清单全 None</summary>
        public delegate TileEffectValue EffectResolver(TileEffectType effect, int pos);

        private readonly ElementRule _row;

        /// <summary>已解析效果清单，与表 effects 列对应；越界兜底第 1 档</summary>
        public readonly IReadOnlyList<TileEffectValue> Effects;

        public ElementRuleSpec(ElementRule row, EffectResolver resolve)
        {
            _row = row;

            IReadOnlyList<TileEffectType> effects = row.Effects;
            IReadOnlyList<int> positions = row.EffectValuePos;

            int count = effects?.Count ?? 0;

            var resolved = new List<TileEffectValue>(count);

            for (int i = 0; i < count; i++)
            {
                // 档位列短时静默补第 1 档，缺列由 TileEffectSpec 报
                int pos = positions != null && i < positions.Count ? positions[i] : 1;

                TileEffectValue effect = resolve != null
                    ? resolve(effects[i], pos)
                    : default;

                resolved.Add(effect);
            }

            Effects = resolved;
        }

        /// <summary>优先级=表主键，只用于诊断与排序</summary>
        public int Priority => _row.Priority;

        /// <summary>结果地块状态；Normal=落回常规格，None=不改动（无匹配兜底）</summary>
        public TileStateType ResultTileType => _row.ResultId;

        /// <summary>必须全含的标签位</summary>
        public ElementTag RequireTags => _row.RequireTag;

        /// <summary>必须都不含的标签位，0=不排除任何</summary>
        public ElementTag ExcludeTags => _row.ExcludeTag;

        public int TemperatureMin => _row.RequireTempMin;

        public int TemperatureMax => _row.RequireTempMax;

        public int WetMin => _row.RequireWetMin;

        public int WetMax => _row.RequireWetMax;

        /// <summary>导电下限，-1=不检查</summary>
        public int ConductivityMin => _row.RequireCondMin;

        /// <summary>本规则是否接受该元素：条件全 AND，温度/湿度闭区间，导电只判下限</summary>
        public bool Match(in ElementValue element)
        {
            if ((element.Tags & RequireTags) != RequireTags) return false;

            if ((element.Tags & ExcludeTags) != 0) return false;

            if (element.Temperature < TemperatureMin) return false;

            if (element.Temperature > TemperatureMax) return false;

            if (element.Wet < WetMin) return false;

            if (element.Wet > WetMax) return false;

            if (element.Conductivity < ConductivityMin) return false;

            return true;
        }
    }
}
