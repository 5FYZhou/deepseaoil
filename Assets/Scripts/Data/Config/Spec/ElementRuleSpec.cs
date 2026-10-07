using System.Collections.Generic;
using cfg.demo;
using DeepseaOil.Logic.Grid;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一条元素反应规则：<b>匹配条件 ＋ 结果状态 ＋ 效果清单</b>。持有 <c>element_rule</c> 表行，构造期就把"效果号 ＋ 档位"解析成已定值的 <see cref="DeepseaOil.Logic.Grid.TileEffect"/>。
    /// </summary>
    /// <remarks>
    /// <b>顺序即优先级</b>：表里 <c>priority</c> 是主键，但<u>匹配</u>按传入列表的先后顺序进行（上游就是这么写的：第一个命中的规则胜出，<c>priority</c> 列本身不参与判定）。出规则清单的人负责按优先级排好。
    /// <para><b>效果清单在构造期解析一次</b>（"帧内零查表"）：此后 <see cref="Effects"/> 是可以直接丢给 <c>ITileResolver.Apply</c> 的定值列表，Logic 层不再认识效果号与档位。</para>
    /// <para>越界档位会在构造期由 <c>TileEffectSpec</c> 报 Warning 并夹到第 1 档 —— 那是配置事故，不该等到第一次命中才暴露。</para>
    /// </remarks>
    public sealed class ElementRuleSpec
    {
        /// <summary>取"某效果的第 pos 档"的解析器：由 <c>ConfigModule</c> 提供（效果号 → 效果包装件）。可为 <c>null</c>（表里没有 effect 表时：效果清单退化成"全是 None"）。</summary>
        public delegate DeepseaOil.Logic.Grid.TileEffect EffectResolver(TileEffectType effect, int pos);

        private readonly ElementRule _row;

        /// <summary>已解析的效果清单（与表里的 <c>effects</c> 逐条对应，越界的那条已兜底成第 1 档）。</summary>
        public readonly IReadOnlyList<DeepseaOil.Logic.Grid.TileEffect> Effects;

        public ElementRuleSpec(ElementRule row, EffectResolver resolve)
        {
            _row = row;

            IReadOnlyList<TileEffectType> effects = row.Effects;
            IReadOnlyList<int> positions = row.EffectValuePos;

            int count = effects?.Count ?? 0;

            var resolved = new List<DeepseaOil.Logic.Grid.TileEffect>(count);

            for (int i = 0; i < count; i++)
            {
                // 档位列比效果列短时按第 1 档取：缺列是配置事故，报在 TileEffectSpec 那边（它会看到 pos=1 是合法的，故这里静默补 1）。
                int pos = positions != null && i < positions.Count ? positions[i] : 1;

                DeepseaOil.Logic.Grid.TileEffect effect = resolve != null
                    ? resolve(effects[i], pos)
                    : default;

                resolved.Add(effect);
            }

            Effects = resolved;
        }

        /// <summary>优先级（= 表主键）。<b>只用于诊断与排序</b>：匹配顺序由传入列表的先后决定。</summary>
        public int Priority => _row.Priority;

        /// <summary>结果地块状态；<see cref="TileStateType.Normal"/> = 落回常规格，<see cref="TileStateType.None"/> = 不改动（无匹配时的兜底行）。</summary>
        public TileStateType ResultTileType => _row.ResultId;

        /// <summary>必须<b>全部</b>含有的标签位。</summary>
        public ElementTag RequireTags => _row.RequireTag;

        /// <summary>必须<b>一个都不含</b>的标签位（<c>0</c> = 不排除任何标签）。</summary>
        public ElementTag ExcludeTags => _row.ExcludeTag;

        public int TemperatureMin => _row.RequireTempMin;

        public int TemperatureMax => _row.RequireTempMax;

        public int WetMin => _row.RequireWetMin;

        public int WetMax => _row.RequireWetMax;

        /// <summary>导电下限；<c>-1</c> = 不检查（表里的约定，注释原文「最小导电要求(-1代表没有」）。</summary>
        public int ConductivityMin => _row.RequireCondMin;

        /// <summary>本规则是否接受这份元素：条件全部 AND，温度 / 湿度是闭区间，导电只判下限。</summary>
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
