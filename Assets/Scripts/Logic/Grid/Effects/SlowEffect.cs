using cfg.demo;
using DeepseaOil.Logic.Combat;
using UnityEngine;

namespace DeepseaOil.Logic.Grid.Effects
{
    public sealed class SlowEffect : ITileEffect
    {
        public TileEffectType Type => TileEffectType.Slow;

        public void Apply(
            Vector3Int cell,
            EffectValueSingle value,
            in TileEffectContext ctx)
        {
            if (!ctx.Registry.TryGetIn(cell, out var targets))
                return;

            float multiplier = value.value1;

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ISlowable slowable)
                {
                    Debug.Log("SlowApply");
                    slowable.SetSlowMultiplier(multiplier);
                }
            }
        }
    }
}