using System.Collections.Generic;
using cfg.dso;

namespace DeepseaOil.Data
{
    public sealed class LevelSpec
    {
        private readonly LevelWave _row;
        private readonly IReadOnlyList<WaveSpec> _waves;

        public LevelSpec(LevelWave row, IReadOnlyList<WaveSpec> waves)
        {
            _row = row;
            _waves = waves;
        }

        public int Id => _row.Id;

        public IReadOnlyList<WaveSpec> Waves => _waves;
    }
}
