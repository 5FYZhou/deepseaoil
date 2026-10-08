using System.Collections.Generic;
using UnityEngine;
using cfg.dso;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>一次规则匹配的结果：命中与否、结果状态、效果清单，以及命中行的下标与优先级</summary>
    /// <remarks>命中判据只有一条：清单里第一条全条件满足的行。未命中时结果恒为 None、效果恒为 null —— 不许拿最后一行当兜底，否则"没配规则"会伪装成"配了规则"。</remarks>
    public readonly struct ReactionMatch
    {
        /// <summary>是否真有规则命中；false=清单为空或没有一行满足</summary>
        public readonly bool Matched;

        /// <summary>命中的结果地块状态；未命中恒为 None</summary>
        public readonly TileStateType Result;

        /// <summary>命中的效果清单；未命中恒为 null</summary>
        public readonly IReadOnlyList<TileEffectValue> Effects;

        /// <summary>命中行在清单里的下标，-1=无命中</summary>
        public readonly int RuleIndex;

        /// <summary>命中行的优先级（表主键），只用于诊断；未命中为 0</summary>
        public readonly int Priority;

        public ReactionMatch(
            bool matched,
            TileStateType result,
            IReadOnlyList<TileEffectValue> effects,
            int ruleIndex,
            int priority)
        {
            Matched = matched;
            Result = result;
            Effects = effects;
            RuleIndex = ruleIndex;
            Priority = priority;
        }

        /// <summary>命中行的效果条数，无效果与未命中都是 0</summary>
        public int EffectCount => Effects?.Count ?? 0;

        /// <summary>无命中：None + 无效果 + 下标 -1</summary>
        public static ReactionMatch None => new ReactionMatch(false, TileStateType.None, null, -1, 0);
    }

    /// <summary>一次落地的完整反应上下文，供诊断通道按固定格式输出</summary>
    /// <remarks>值类型，构造零分配；只有真的输出时才会拼字符串（见 ReactionResolver.LogTrace）。原格元素是合成前的旧值，不是合成写回后的值。</remarks>
    public readonly struct ReactionTrace
    {
        /// <summary>合成前的原格元素</summary>
        public readonly ElementValue OldTile;

        /// <summary>本次落地的球元素</summary>
        public readonly ElementValue Ball;

        /// <summary>两份元素合成后的结果</summary>
        public readonly ElementValue Combined;

        public readonly ReactionMatch Match;

        public ReactionTrace(in ElementValue oldTile, in ElementValue ball, in ElementValue combined, in ReactionMatch match)
        {
            OldTile = oldTile;
            Ball = ball;
            Combined = combined;
            Match = match;
        }

        /// <summary>固定格式的一行追踪</summary>
        public string Describe()
        {
            string verdict = Match.Matched
                ? $"命中规则 #{Match.Priority} [生成地块: {Match.Result}({(int)Match.Result}), 效果数: {Match.EffectCount}]"
                : "无规则命中";

            return $"[Reaction] 原格元素 (T:{OldTile.Temperature}, W:{OldTile.Wet}, C:{OldTile.Conductivity}, Tags:{OldTile.Tags}) " +
                   $"+ 球元素 (T:{Ball.Temperature}, W:{Ball.Wet}, C:{Ball.Conductivity}, Tags:{Ball.Tags}) " +
                   $"= 合成元素 (T:{Combined.Temperature}, W:{Combined.Wet}, C:{Combined.Conductivity}) -> {verdict}";
        }
    }

    /// <summary>规则匹配：在一份元素上找第一条命中的规则，纯函数（清单由调用方给）</summary>
    /// <remarks>顺序即优先级：不按 priority 排序，清单顺序即判定顺序（读取顺序由 ConfigModule.GetElementRules() 保证）。无命中时返回 ReactionMatch.None，调用方据此"什么都不做"，效果不再照常发生。输出通道由 TraceEnabled 控制，关掉时零分配。</remarks>
    public static class ReactionResolver
    {
        /// <summary>反应追踪开关：诊断脚手架置 true 后每次落地输出一行 [Reaction]；正式构建保持 false</summary>
        public static bool TraceEnabled;

        /// <summary>匹配一份元素，返回命中行（或 None）</summary>
        public static ReactionMatch Match(IReadOnlyList<ElementRuleSpec> rules, in ElementValue element)
        {
            if (rules == null || rules.Count == 0) return ReactionMatch.None;

            for (int i = 0; i < rules.Count; i++)
            {
                if (!rules[i].Match(in element)) continue;

                return new ReactionMatch(true, rules[i].ResultTileType, rules[i].Effects, i, rules[i].Priority);
            }

            // 走到这里就是真没命中：不返回任何行，也不给效果。
            return ReactionMatch.None;
        }

        /// <summary>按开关输出一行结构化追踪；关掉时不拼字符串、不分配</summary>
        public static void LogTrace(in ReactionTrace trace)
        {
            if (!TraceEnabled) return;

            Debug.Log(trace.Describe());
        }
    }
}
