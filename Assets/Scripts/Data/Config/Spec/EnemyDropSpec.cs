using cfg.dso;

namespace DeepseaOil.Data
{
    public sealed class EnemyDropSpec
    {
        private readonly EnemyDrop _row;

        public EnemyDropSpec(EnemyDrop row)
        {
            _row = row;
        }

        public int Id => _row.Id;
        public int EnemyId => _row.EnemyId;
        public SeedType SeedType => _row.SeedType;
        public int Weight => _row.Weight;
        public float Probability => _row.Probability;
    }
}
