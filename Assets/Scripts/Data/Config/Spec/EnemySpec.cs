using cfg.dso;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>敌人种类取值边界，合并 enemy 表行与 CharacterConfig；行不出 Data 层</summary>
    /// <remarks>
    /// 半径=世界单位，速度=单位/秒，FlashHz=Hz；StopDistance=进入即不再压上，ChaseRange=超出即放弃追击。
    /// 冲刺/8向吸附/外力显式清零，否则继承玩家默认值；受击滑停用 knockback_decay，喂 turnDecayRate/hurtDecay。
    /// </remarks>
    public sealed class EnemySpec
    {
        private readonly Enemy _row;

        public EnemySpec(Enemy row)
        {
            _row = row;

            var config = ScriptableObject.CreateInstance<CharacterConfig>();

            config.name = $"EnemyConfig_{row.Id}";
            config.Name = row.Name;
            config.moveSpeed = row.MaxSpeed;
            config.snapToEightDirections = false;
            config.moveAcceleration = row.Acceleration;
            config.turnDecayRate = row.KnockbackDecay;
            config.hurtDecay = row.KnockbackDecay;
            config.extraForceScale = 0f;
            config.dashSpeed = 0f;
            config.dashDuration = 0f;

            Config = config;
        }

        public int Id => _row.Id;

        public string Name => _row.Name;

        public float Radius => _row.Radius;

        public float MaxSpeed => _row.MaxSpeed;

        public float StopDistance => _row.StopDistance;

        public float ChaseRange => _row.ChaseRange;

        public int Hp => _row.Hp;

        /// <summary>受击闪烁频率（Hz）</summary>
        /// <remarks>表已不再配这一列：频率是观感参数，取 VisualPalette.enemyFlashHz，改它不用导表</remarks>
        public float FlashHz => ConfigModule.Visuals != null ? ConfigModule.Visuals.enemyFlashHz : 4f;

        /// <remarks>运行期按种类造一份、每只各持一份；只有装配链消费</remarks>
        public CharacterConfig Config { get; }
    }
}
