using DeepseaOil.Logic.Combat;

namespace DeepseaOil.Logic.Grid
{
    public readonly struct TileEffectContext
    {
        public readonly EnemyCellRegistry Registry;

        public TileEffectContext(EnemyCellRegistry registry)
        {
            Registry = registry;
        }
    }
}