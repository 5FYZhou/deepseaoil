using cfg.dso;

namespace DeepseaOil.Data
{
    public sealed class AudioMapSpec
    {
        private readonly AudioMap _row;

        public AudioMapSpec(AudioMap row)
        {
            _row = row;
        }

        public cfg.dso.AudioId Id => _row.Id;

        public string FileName => _row.FileName;
    }
}
