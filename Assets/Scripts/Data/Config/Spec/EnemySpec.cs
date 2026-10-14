using cfg.dso;
using DeepseaOil.Data.Settings;
using UnityEngine;

namespace DeepseaOil.Data
{

    // 表给耐久与极速，运动学与形变走 EnemyTuning
    public sealed class EnemySpec
    {
        private readonly Enemy _row;
        private readonly EnemyTuningEntry _tuning;

        public EnemySpec(Enemy row, EnemyTuningEntry tuning)
        {
            _row = row;
            _tuning = tuning;

            var config = ScriptableObject.CreateInstance<CharacterConfig>();

            config.name = $"EnemyConfig_{row.Id}";
            config.Name = row.Name;
            config.moveSpeed = row.MaxSpeed;
            config.snapToEightDirections = false;
            config.moveAcceleration = tuning.acceleration;
            config.turnDecayRate = tuning.knockbackDecay;
            config.hurtDecay = tuning.knockbackDecay;
            config.extraForceScale = 0f;
            config.dashSpeed = 0f;
            config.dashDuration = 0f;

            Config = config;
        }

        public int Id => _row.Id;

        public string Name => _row.Name;

        public float Radius => _tuning.radius;

        public float MaxSpeed => _row.MaxSpeed;

        public float StopDistance => _tuning.stopDistance;

        public float ChaseRange => _tuning.chaseRange;

        public float VisionRange => _tuning.visionRange;

        public float SustainRange => _tuning.sustainRange;

        public float LoseDelay => _tuning.loseDelay;

        public int Hp => _row.Hp;

        /// <summary>贴身一次对玩家造成的伤害；敌人伤害的唯一权威，玩家表的 contact_damage 不用在这条链上</summary>
        public int ContactDamage => _row.ContactDamage;

        /// <summary>受击形变调参：EnemyTuning 行对象，改它当场生效</summary>
        /// <remarks>永不返回 null：漏配就现造一份默认值，形变照旧而不是崩</remarks>
        public SquashTuning Squash => _tuning.squash ?? (_tuning.squash = new SquashTuning());

        public CharacterConfig Config { get; }
    }
}
