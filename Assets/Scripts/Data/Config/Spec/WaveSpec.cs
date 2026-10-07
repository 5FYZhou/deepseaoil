using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>一波敌人的取值边界：持有 <c>wave</c> 表行，暴露被消费的语义点（行不对外暴露）。</summary>
    public sealed class WaveSpec
    {
        private readonly Wave _row;

        public WaveSpec(Wave row)
        {
            _row = row;
        }

        public int Id => _row.Id;

        public int EnemiesPerWave => _row.EnemiesPerWave;

        /// <summary>同一波内两只敌人之间的间隔（秒）。</summary>
        public float SpawnInterval => _row.SpawnInterval;

        /// <summary>开局到第一波的等待（秒）。</summary>
        public float InitialDelay => _row.InitialDelay;

        /// <summary>清完一波到下一波的等待（秒）。</summary>
        public float RespawnDelay => _row.RespawnDelay;

        /// <summary>出生环半径（世界单位）。</summary>
        public float SpawnRadius => _row.SpawnRadius;
    }
}
