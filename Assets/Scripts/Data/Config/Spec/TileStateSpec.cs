using System.Collections.Generic;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>一个格子状态的取值边界：持有 <c>tile_state</c> 表行，暴露被消费的语义点，并在构造期把效果清单解析成已定值。</summary>
    /// <remarks>
    /// 行不对外暴露（生成行不出 Data 层）。
    /// <para><b>旧字段已删</b>（§7 判决 2）：<c>slow_factor</c> / <c>enter_damage</c> / <c>enter_knockback</c> 三列随上游表改版消失，"这个状态做什么"改由 <c>effects</c> ＋ <c>effectValuePos</c> 表达。</para>
    /// <para><b>状态的元素四件</b>（<see cref="Element"/>）是 D9 的出处：切进这个状态时把它刷到格子上作初值。</para>
    /// <para><b>效果清单是已定值的 <see cref="TileEffectValue"/></b>：档位在构造期就由传入的解析器选好（<c>TileEffectSpec.GetEffect</c>），状态机拿到的是可以直接提交的取值，
    /// 不需要认识 <c>tile_effect</c> 表的列，也不需要认识档位号。</para>
    /// </remarks>
    public sealed class TileStateSpec
    {
        private readonly TileState _row;

        /// <summary>已解析的效果清单（表里 <c>effects</c> 与 <c>effectValuePos</c> 逐条配对、档位已选定）。</summary>
        public readonly IReadOnlyList<TileEffectValue> EnterEffects;

        /// <param name="row">表行。</param>
        /// <param name="resolve">取"某效果的第 pos 档"的解析器；为 <c>null</c> 时效果清单退化成"全是 None"（逻辑层单跑测试的场合）。</param>
        public TileStateSpec(TileState row, ElementRuleSpec.EffectResolver resolve = null)
        {
            _row = row;

            IReadOnlyList<TileEffectType> effects = row.Effects;
            IReadOnlyList<int> positions = row.EffectValuePos;

            int count = effects?.Count ?? 0;

            var resolved = new List<TileEffectValue>(count);

            for (int i = 0; i < count; i++)
            {
                int pos = positions != null && i < positions.Count ? positions[i] : 1;

                TileEffectValue effect = resolve != null ? resolve(effects[i], pos) : default;

                // None 不入清单：表里用"效果 0"表达"本行无效果"，把它排进逐帧执行的清单只是白白走一趟。
                if (effect.Kind == TileEffectKind.None) continue;

                resolved.Add(effect);
            }

            EnterEffects = resolved;
        }

        public TileStateType Id => _row.Id;

        public string Name => _row.Name;

        /// <summary>持续时间（秒）；<c>&lt;= 0</c> = 永久（只能被别的状态顶掉）。</summary>
        public float Duration => _row.Duration;

        /// <summary>是否会把自身传播给相邻格（<b>本轮只读不做</b>，D13"先保持，不动"）。</summary>
        public bool WillSpread => _row.WillSpread;

        /// <summary>是否参与元素反应（同上，本轮只读）。</summary>
        public bool CanReact => _row.CanReact;

        /// <summary>地形元素四件（温度 / 湿度 / 导电 / 标签）：D9 的初值来源。</summary>
        public ElementValue Element => new ElementValue(ElementType.Environment, _row.Tags, _row.Temp, _row.Wet, _row.Cond);

        /// <summary>
        /// 效果清单的触发节拍（秒）：状态自己按这个间隔重复提交 <see cref="EnterEffects"/>。<c>0</c> = 每个 Tick 都提交（泥浆的减速靠的就是"每帧续命"）。
        /// </summary>
        /// <remarks>取该状态效果里各 <c>interval</c> 的最小正值；<b>"级别 / 档位"已在构造期消解</b>，这里只剩一个节拍数。持续伤害自己的扣血节奏在效果的 <c>Interval</c> 上，与本值无关。</remarks>
        public float TickInterval
        {
            get
            {
                float interval = 0f;

                for (int i = 0; i < EnterEffects.Count; i++)
                {
                    float candidate = EnterEffects[i].Interval;

                    if (candidate <= 0f) continue;

                    if (interval <= 0f || candidate < interval) interval = candidate;
                }

                return interval;
            }
        }
    }
}
