using System.Collections.Generic;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Data
{
    /// <summary>地块效果取值边界，持有 tile_effect 表行，在数据层完成档位解析</summary>
    /// <remarks>档位是配置概念，多档在此选好，Logic 只见已定值 TileEffectValue，ITileResolver.Apply 签名不随效果数增长。取档口径 pos 为 1-based（同 effectValuePos 列），越界夹到第 1 档并报 Warning。持生成行 cfg.dso.TileEffect，产出已定值 TileEffectValue</remarks>
    public sealed class TileEffectSpec
    {
        private readonly cfg.dso.TileEffect _row;

        public TileEffectSpec(cfg.dso.TileEffect row)
        {
            _row = row;
        }

        public TileEffectType Id => _row.Id;

        public string Name => _row.Name;

        /// <summary>表内注释原文，排错用</summary>
        public string Tip => _row.Tip;

        /// <summary>声明了几档，取四列长度最小值</summary>
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

        /// <summary>取第 pos 档（1-based），越界夹第 1 档并报 Warning</summary>
        public TileEffectValue GetEffect(int pos)
        {
            return GetEffect(pos, out _);
        }

        /// <summary>取第 pos 档（1-based）；inRange=false=夹取后兜底值</summary>
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
                // 四列一档都没有=这行还没填，给 default 表示效果不存在
                return default;
            }

            return Build(index);
        }

        /// <summary>取第 index 档（0-based，调用方保证在范围内）</summary>
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

                // None / Skid / Block / Fixed：本轮不做，保留表行，解析成无效果
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
