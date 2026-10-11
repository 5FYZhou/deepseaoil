using System.Collections.Generic;
using cfg.dso;

namespace DeepseaOil.Data
{

    public sealed class WaveSpec
    {
        private readonly Wave _row;
        private readonly IReadOnlyList<WaveEnemySpec> _enemies;


        public WaveSpec(Wave row, IReadOnlyList<WaveEnemySpec> enemies)
        {
            _row = row;
            _enemies = enemies;
        }

        public int Id => _row.Id;

        public string Name => _row.Name;

        public float PrepTime => _row.PrepTime;

        public float BattleTime => _row.BattleTime;

        public float SettleTime => _row.SettleTime;


        public IReadOnlyList<WaveEnemySpec> Enemies => _enemies;

        public float SpawnInterval => _row.SpawnInterval;

        public float SpawnRadius => _row.SpawnRadius;

        public SeedType GrantSeed => _row.GrantSeed;
    }
}
