using cfg.demo;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.Effects;
using UnityEngine;

public interface ITileEffect
{
    TileEffectType Type { get; }

    void Apply(
        Vector3Int cell,
        EffectValueSingle value,
        in TileEffectContext ctx);
}