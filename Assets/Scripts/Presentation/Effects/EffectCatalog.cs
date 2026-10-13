namespace DeepseaOil.Presentation.Effects
{
    internal enum EffectDriverKind
    {
        Particle = 0,

        Shatter = 1,

        Highlight = 2,

        Flash = 3,

        ScreenShake = 4,

        Tint = 5,

        ScalePunch = 6,
    }

    internal readonly struct EffectSpec
    {
        public readonly EffectId Id;

        /// <summary>资源 Key，默认 effects/+枚举名</summary>
        public readonly string Key;

        public readonly EffectDriverKind DriverKind;

        public readonly bool IsSingleton;

        /// <summary>池上限，超了丢弃并警告</summary>
        public readonly int MaxSize;

        /// <summary>预热数；&lt;0 按 0</summary>
        public readonly int Prewarm;

        public EffectSpec(
            EffectId id,
            EffectDriverKind driverKind = EffectDriverKind.Particle,
            bool isSingleton = false,
            int maxSize = 16,
            int prewarm = 0,
            string key = null)
        {
            Id = id;
            Key = string.IsNullOrEmpty(key) ? EffectCatalog.KeyPrefix + id : key;
            DriverKind = driverKind;
            IsSingleton = isSingleton;
            MaxSize = maxSize > 0 ? maxSize : 1;
            Prewarm = prewarm < 0 ? 0 : prewarm;
        }

        public override string ToString()
            => $"{Id}(kind={DriverKind} key={Key} singleton={IsSingleton} max={MaxSize} prewarm={Prewarm})";
    }

    /// <summary>特效装配表：加一行多一个</summary>
    /// <remarks>未登记的 EffectId（只剩 Shake）Play 打 LogError 返回 None；None 不播不报错。</remarks>
    internal static class EffectCatalog
    {
        internal const string KeyPrefix = "effects/";

        // 代码默认骨架：maxSize / prewarm 被 EffectTuning 同 Id 行覆盖
        // 顺序与 EffectId 枚举一致；Shake(3) 刻意不注册，M3 用例守失败路径
        private static readonly EffectSpec[] Specs =
        {
            new EffectSpec(EffectId.BurstSparks,  EffectDriverKind.Particle,    isSingleton: false, maxSize: 32, prewarm: 8),

            new EffectSpec(EffectId.Flash,        EffectDriverKind.Tint,        isSingleton: false, maxSize: 32, prewarm: 0),

            // EffectId.Shake = 3 刻意不注册

            new EffectSpec(EffectId.ScreenShake,  EffectDriverKind.ScreenShake, isSingleton: true),

            new EffectSpec(EffectId.MudSplash,    EffectDriverKind.Particle,    isSingleton: false, maxSize: 16, prewarm: 4),

            new EffectSpec(EffectId.Shatter,      EffectDriverKind.Shatter,     maxSize: 16, prewarm: 0),

            new EffectSpec(EffectId.Highlight,    EffectDriverKind.Highlight,   isSingleton: true, maxSize: 1, prewarm: 0),

            new EffectSpec(EffectId.SteamBlast,   EffectDriverKind.Particle,    isSingleton: false, maxSize: 16, prewarm: 4),

            new EffectSpec(EffectId.ChainShock,   EffectDriverKind.Particle,    isSingleton: false, maxSize: 64, prewarm: 32),

            new EffectSpec(EffectId.FlameEmbers,  EffectDriverKind.Particle,    isSingleton: false, maxSize: 64, prewarm: 16),

            new EffectSpec(EffectId.SlowTint,     EffectDriverKind.Tint,        isSingleton: false, maxSize: 32, prewarm: 0),

            new EffectSpec(EffectId.Squash,       EffectDriverKind.ScalePunch,  maxSize: 32),

            new EffectSpec(EffectId.Dust,         EffectDriverKind.Particle,    isSingleton: false, maxSize: 16, prewarm: 0),
        };

        internal static EffectSpec[] BuildSpecs(EffectTuning tuning)
        {
            var result = new EffectSpec[Specs.Length];

            for (int i = 0; i < Specs.Length; i++)
            {
                EffectSpec spec = Specs[i];

                result[i] = spec;

                if (tuning == null || tuning.pools == null) continue;

                for (int j = 0; j < tuning.pools.Length; j++)
                {
                    EffectTuning.PoolConfig pool = tuning.pools[j];

                    if (pool.Id != (int)spec.Id || pool.MaxSize <= 0) continue;

                    result[i] = new EffectSpec(spec.Id, spec.DriverKind, spec.IsSingleton, pool.MaxSize, pool.Prewarm, spec.Key);

                    break;
                }
            }

            return result;
        }

        internal static bool TryGet(EffectId id, out EffectSpec spec)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                if (Specs[i].Id == id)
                {
                    spec = Specs[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }
    }
}
