using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个地块效果的取值边界：持有 <c>tile_effect</c> 表行（<c>cfg.demo.TileEffect</c>），<b>并在数据层完成"档位解析"</b>。
    /// </summary>
    /// <remarks>
    /// 核心决议（§7）：<b>档位是配置概念，不是行为概念</b> —— 多档在这里选好，Logic 层只看到已定值的 <c>DeepseaOil.Logic.Grid.TileEffect</c>。
    /// 于是 <c>ITileResolver.Apply</c> 的签名永远不随效果数量增长。
    /// <para><b>越界在这里报</b>（一条 Warning），不往 Logic 层漏：表里少配一档是本类的责任，不是执行者的责任。</para>
    /// <para><b>取档口径</b>：<c>pos</c> 是 <b>1-based</b> 档位号（与 <c>effectValuePos</c> 列同义）；越界（<c>pos &lt; 1</c> 或超出该效果的档数）时<b>夹到第 1 档 ＋ 报一条 Warning</b>，而不是静默返回"没有效果" ——
    /// 配表少一档应该表现为"这一档没生效"（能被看见），而不是"这个效果整条消失了"（查不出来）。</para>
    /// <para><b>本文件一律写全名</b>：生成行 <c>cfg.demo.TileEffect</c> 与逻辑值 <c>DeepseaOil.Logic.Grid.TileEffect</c> 同名，
    /// 靠 <c>using</c> 去猜哪个是哪个只会让下一个人踩同一个坑。</para>
    /// </remarks>
    public sealed class TileEffectSpec
    {
        private readonly cfg.demo.TileEffect _row;

        public TileEffectSpec(cfg.demo.TileEffect row)
        {
            _row = row;
        }

        public cfg.demo.TileEffectType Id => _row.Id;

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

        /// <summary>取第 <paramref name="pos"/> 档（1-based）并解析成已定值的 <see cref="DeepseaOil.Logic.Grid.TileEffect"/>；越界时夹到第 1 档 ＋ 报 Warning。</summary>
        public DeepseaOil.Logic.Grid.TileEffect GetEffect(int pos)
        {
            return GetEffect(pos, out _);
        }

        /// <summary>取第 <paramref name="pos"/> 档（1-based）；<paramref name="inRange"/> 为 <c>false</c> 表示这次是夹取后的兜底值。</summary>
        public DeepseaOil.Logic.Grid.TileEffect GetEffect(int pos, out bool inRange)
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
        /// <remarks><b>枚举写全名 <c>cfg.demo.TileEffectType</c></b>：本层同时 <c>using cfg.demo</c>，而 <c>cfg.demo</c> 里也有一个叫 <c>TileEffect</c> 的生成行 —— 短名会让 <c>TileEffectType</c> 的归属看起来像是那个行类型的一部分。</remarks>
        private DeepseaOil.Logic.Grid.TileEffect Build(int index)
        {
            float v1 = Value(_row.Value1, index);
            float v2 = Value(_row.Value2, index);
            float interval = Value(_row.Interval, index);

            switch (Id)
            {
                case cfg.demo.TileEffectType.Slow:
                    return DeepseaOil.Logic.Grid.TileEffect.Slow(v1, v2);

                case cfg.demo.TileEffectType.Slid:
                    return DeepseaOil.Logic.Grid.TileEffect.Slide((int)v1, v2);

                case cfg.demo.TileEffectType.KnockBack:
                    return DeepseaOil.Logic.Grid.TileEffect.KnockBack(v1);

                case cfg.demo.TileEffectType.Numbness:
                    return DeepseaOil.Logic.Grid.TileEffect.Numbness(v1);

                case cfg.demo.TileEffectType.DamageInstant:
                    return DeepseaOil.Logic.Grid.TileEffect.InstantDamage(v1);

                case cfg.demo.TileEffectType.DamageOverTime:
                    return DeepseaOil.Logic.Grid.TileEffect.DamageOverTime(v1, interval, 0f);

                case cfg.demo.TileEffectType.InheritedTW:
                    return DeepseaOil.Logic.Grid.TileEffect.InheritElement(v1, v2, interval);

                case cfg.demo.TileEffectType.ClearPlant:
                    return DeepseaOil.Logic.Grid.TileEffect.ClearPlants((int)v1);

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
