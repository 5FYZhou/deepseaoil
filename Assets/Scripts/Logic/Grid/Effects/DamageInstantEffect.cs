using cfg.demo;
using DeepseaOil.Logic.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid.Effects
{
    public sealed class DamageInstantEffect : ITileEffect
    {
        public TileEffectType Type => TileEffectType.DamageInstant;

        private readonly EnemyCellRegistry _registry;
        private readonly GridGeometry _geometry;

        private readonly List<IEffectTarget> _scratch = new();

        public DamageInstantEffect(
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

            float amount = value.value1;

            for (int i = 0; i < _scratch.Count; i++)
            {
                IEffectTarget target = _scratch[i];

                if (target == null || target.IsDead)
                    continue;

                if (target is not IDamageable damageable)
                    continue;

                Damage damage = Damage.At(
                    center,
                    target.Position,
                    amount,
                    DamageSource.Tile,
                    0f);

                damageable.TakeDamage(in damage);
            }

            _scratch.Clear();
        }
    }
}