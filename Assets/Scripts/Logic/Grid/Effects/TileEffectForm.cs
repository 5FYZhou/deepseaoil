
using cfg.demo;
using DeepseaOil.Data;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid.Effects
{
    public struct EffectValueSingle
    {
        public readonly TileEffectType Type;
        public readonly string Name;
        public readonly float value1;
        public readonly float value2;
        public readonly float interval;
        public readonly bool flag;

        public EffectValueSingle(TileEffectType effectType, string name,
            float v1, float v2, float i, bool f)
        {
            this.Type = effectType;
            this.Name = name;
            this.value1 = v1;
            this.value2 = v2;
            this.interval = i;
            this.flag = f;
        }

    }

    /// <summary>
    /// 存储效果数据
    /// 存储效果实现（需注册）
    /// 应用某格子上的效果，发生反应时(OnEnter)的效果和地块的效果(OnTick)都调用Execute(Vector3Int cell, in TileEffectInfoSpec info, in TileEffectContext ctx)
    /// </summary>
    public sealed class TileEffectExecutor
    {
        // 效果数据
        public readonly Dictionary<TileEffectType, TileEffectSpec> _effectsSpec = new();
        // 效果类型-效果实现
        private readonly Dictionary<TileEffectType, ITileEffect> _effects = new();

        public TileEffectExecutor(IReadOnlyList<TileEffectSpec> effects)
        {
            foreach (var effect in effects)
            {
                _effectsSpec[effect.Type] = effect;
            }
        }
        
        private EffectValueSingle GetSingleValue(TileEffectType type, int Pos)
        {
            var effect = _effectsSpec[type];
            int minCount = Mathf.Min(effect.values1.Count, effect.values2.Count, effect.interval.Count, effect.flags.Count);
            int idx = Pos - 1;
            if (idx < 0 || idx >= minCount)
            {
                Debug.LogWarning($"{type.ToString()} 级别(位置) {idx}未获得数据");
                return default;
            }
            return new(type, effect.Name,
                effect.values1[idx], effect.values2[idx], effect.interval[idx], effect.flags[idx]);
        }

        public void Register(ITileEffect effect)
        {
            _effects[effect.Type] = effect;
        }

        public void Execute(Vector3Int cell, in TileEffectInfoSpec info, in TileEffectContext ctx)
        {
            if (info.Effects == null || info.ValuePos == null)
                return;

            int count = Mathf.Min(info.Effects.Count, info.ValuePos.Count);

            for (int i = 0; i < count; i++)
            {
                TileEffectType type = info.Effects[i];
                var v = GetSingleValue(type, info.ValuePos[i]);

                if (!_effects.TryGetValue(type, out var effect))
                    continue;

                effect.Apply(cell, v, in ctx);
            }
        }
    }
}
