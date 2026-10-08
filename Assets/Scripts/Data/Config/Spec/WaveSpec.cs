using cfg.dso;

namespace DeepseaOil.Data
{
    public sealed class WaveSpec
    {
        private readonly Wave _row;

        public WaveSpec(Wave row)
        {
            _row = row;
        }

        public int Id => _row.Id;

        public int EnemiesPerWave => _row.EnemiesPerWave;

        /// <summary>同一波内两只敌人的间隔（秒）</summary>
        public float SpawnInterval => _row.SpawnInterval;

        /// <summary>开局到第一波的等待（秒）</summary>
        public float InitialDelay => _row.InitialDelay;

        /// <summary>清完一波到下一波的等待（秒）</summary>
        public float RespawnDelay => _row.RespawnDelay;

        public float SpawnRadius => _row.SpawnRadius;
    }
}
