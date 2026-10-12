using cfg.dso;

namespace DeepseaOil.Data
{
    // 一波里的一个怪种：wave_enemy 表一行的只读包装
    public sealed class WaveEnemySpec
    {
        private readonly WaveEnemy _row;

        public WaveEnemySpec(WaveEnemy row)
        {
            _row = row;
        }

        public int Id => _row.Id;

        public int WaveId => _row.WaveId;

        public int EnemyId => _row.EnemyId;

        public int Count => _row.Count;
    }
}
