using cfg.demo;
using DeepseaOil.Logic.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid.Effects
{
    public sealed class KnockBackEffect : ITileEffect
    {
        public TileEffectType Type => TileEffectType.KnockBack;

        private readonly EnemyCellRegistry _registry;
        private readonly GridGeometry _geometry;

        private readonly List<IEffectTarget> _scratch = new();

        public KnockBackEffect(
            EnemyCellRegistry registry,
            GridGeometry geometry)
        {
            _registry = registry;
            _geometry = geometry;
        }

        public void Apply(
            Vector3Int cell,
            EffectValueSingle value,
            in TileEffectContext ctx)
        {
            if (!_registry.TryGetIn(
                    cell,
                    out List<IEffectTarget> targets))
            {
                return;
            }

            if (targets.Count == 0)
                return;

            _scratch.Clear();
            _scratch.AddRange(targets);

            Vector2 center = _geometry.CellCenter(cell);
            float force = value.value1;

            for (int i = 0; i < _scratch.Count; i++)
            {
                IEffectTarget target = _scratch[i];

                if (target == null || target.IsDead)
                    continue;

                if (target is not IKnockBackable knockbackable)
                    continue;

                Vector2 direction =
                    (target.Position - center).normalized;

                knockbackable.ApplyKnockback(
                    direction * force);
            }

            _scratch.Clear();
        }
    }
}