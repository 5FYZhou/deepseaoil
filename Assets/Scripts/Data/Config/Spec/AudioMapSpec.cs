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

        public AudioId Id
        {
            get
            {
                switch (_row.Id)
                {
                    case cfg.dso.AudioId.BgmNormal:
                        return AudioId.Bgm;
                    case cfg.dso.AudioId.PlayerHurt:
                        return AudioId.PlayerHurt;
                    case cfg.dso.AudioId.PlayerDie:
                        return AudioId.PlayerDie;
                    default:
                        throw new ConfigLoadException($"[Config] audio_map 包含未映射的 AudioId：{_row.Id}");
                }
            }
        }

        public string FileName => _row.FileName;
    }
}
