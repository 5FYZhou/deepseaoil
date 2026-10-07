using System.Collections.Generic;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个地块效果的取值边界：持有 <c>tile_effect</c> 表行，<b>并在数据层完成"档位解析"</b>。
    /// </summary>
    /// <remarks>
    /// 核心决议（§7）：<b>档位是配置概念，不是行为概念</b> —— 多档在这里选好，Logic 层只看到已定值的 <see cref="TileEffectValue"/>。
    /// 于是 <c>ITileResolver.Apply</c> 的签名永远不随效果数量增长。
    /// <para><b>越界在这里报</b>（一条 Warning），不往 Logic 层漏：表里少配一档是本类的责任，不是执行者的责任。</para>
    /// <para><b>取档口径</b>：<c>pos</c> 是 <b>1-based</b> 档位号（与 <c>effectValuePos</c> 列同义）；越界（<c>pos &lt; 1</c> 或超出该效果的档数）时<b>夹到第 1 档 ＋ 报一条 Warning</b>，而不是静默返回"没有效果" ——
    /// 配表少一档应该表现为"这一档没生效"（能被看见），而不是"这个效果整条消失了"（查不出来）。</para>
    /// <para><b>两个名字要分清</b>：本类持的是<b>生成行</b> <c>cfg.demo.TileEffect</c>（表里那一行，四列都是数组），
    /// 产出的是<b>已定值</b> <see cref="TileEffectValue"/>（选好档的一份取值）。行类型写全名是因为它与曾经的同名逻辑类型撞过名，
    /// 全名让"这一处读的是表"一眼可辨。</para>
    /// </remarks>
    public sealed class TileEffectSpec
    {
        private readonly cfg.demo.TileEffect _row;

        public TileEffectSpec(cfg.demo.TileEffect row)
        {
            _row = row;
        }

        public TileEffectType Id => _row.Id;

        public string Name => _row.Name;

        /// <summary>表里的注释原文（"value1=速度倍率" 这类），排错时用。</summary>
        public string Tip => _row.Tip;

        /// <summary>本效果声明了几档（取四列长度的最小值）。</summary>
        public int LevelCount
        {
            get
            {
                if (_row.Value1 == null) return 0;

                int count = _row.Value1.Count;

                if (_row.Value2 != null && _row.Value2.Count < count) count = _row.Value2.Count;
                if (_row.Interval != null && _row.Interval.Count < count) count = _row.Interval.Count;
                if (_row.Flag != null && _row.Flag.Count < count) count = _row.Flag.Count;

                return count;
            }
        }

        /// <summary>取第 <paramref name="pos"/> 档（1-based）并解析成已定值的 <see cref="TileEffectValue"/>；越界时夹到第 1 档 ＋ 报 Warning。</summary>
        public TileEffectValue GetEffect(int pos)
        {
            return GetEffect(pos, out _);
        }

        /// <summary>取第 <paramref name="pos"/> 档（1-based）；<paramref name="inRange"/> 为 <c>false</c> 表示这次是夹取后的兜底值。</summary>
        public TileEffectValue GetEffect(int pos, out bool inRange)
        {
            int count = LevelCount;

            inRange = count > 0 && pos >= 1 && pos <= count;

            int index = inRange ? pos - 1 : 0;

            if (!inRange)
            {
                Debug.LogWarning(
                    $"[Config] 地块效果 {Id}（{Name}）没有第 {pos} 档（共 {count} 档）：按第 1 档兜底。请核对表里的 effectValuePos。");
            }

            if (count == 0)
            {
                // 四列一档都没有：这是"这一行还没填"，不是"越界"。给一个 None，让调用方看见"这个效果不存在"。
                return default;
            }

            return Build(index);
        }

        /// <summary>取第 <paramref name="index"/> 档（0-based，已由调用方保证在范围内）并解析成效果。</summary>
        private TileEffectValue Build(int index)
        {
            float v1 = Value(_row.Value1, index);
            float v2 = Value(_row.Value2, index);
            float interval = Value(_row.Interval, index);

            switch (Id)
            {
                case TileEffectType.Slow:
                    return TileEffectValue.Slow(v1, v2);

                case TileEffectType.Slid:
                    return TileEffectValue.Slide((int)v1, v2);

                case TileEffectType.KnockBack:
                    return TileEffectValue.KnockBack(v1);

                case TileEffectType.Numbness:
                    return TileEffectValue.Numbness(v1);

                case TileEffectType.DamageInstant:
                    return TileEffectValue.InstantDamage(v1);

                case TileEffectType.DamageOverTime:
                    return TileEffectValue.DamageOverTime(v1, interval, 0f);

                case TileEffectType.InheritedTW:
                    return TileEffectValue.InheritElement(v1, v2, interval);

                case TileEffectType.ClearPlant:
                    return TileEffectValue.ClearPlants((int)v1);

                // None / Skid / Block / Fixed：§6 本轮不做，表里也没有引用 —— 保留表行，解析成"无效果"。
                default:
                    return default;
            }
        }

        private static float Value(IReadOnlyList<float> values, int index)
        {
            if (values == null || index < 0 || index >= values.Count) return 0f;

            return values[index];
        }
    }
}
