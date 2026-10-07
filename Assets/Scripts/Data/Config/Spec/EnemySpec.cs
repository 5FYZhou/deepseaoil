using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>一个敌人种类的取值边界：合并「<c>enemy</c> 表行」与「按表值造出的 <c>CharacterConfig</c>」；折算只在本构造里发生一处，表行不对外暴露（生成行不出 Data 层）。</summary>
    /// <remarks>
    /// 数值口径：半径 / 距离用世界单位，速度用单位/秒，<c>FlashHz</c> 用 Hz；<c>StopDistance</c> = 进入即不再压上，<c>ChaseRange</c> = 超出即放弃追击。
    /// 与敌人无关的项显式清零（冲刺、8 向吸附、外力）：<c>CharacterConfig</c> 的字段带默认值，不写就是"继承了玩家那份默认值"，而那种错不报错，只会在将来某处被读到 —— 比如某天有人给敌人加了"冲刺"。
    /// 受击滑停用 <c>knockback_decay</c> 而不是 <c>acceleration</c>：这条列的语义正是"被撞之后速度怎么掉"；同一列同时喂 <c>turnDecayRate</c> 与 <c>hurtDecay</c> 是刻意的。
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

        public float FlashHz => _row.FlashHz;

        /// <remarks>运行期按种类造一份、每只敌人各持一份（改一只不影响另一只）；消费者只有装配链（<c>EnemyLogic</c> 把它交给执行器），状态机读的是执行器折算出的六个标量，不是本属性。</remarks>
        public CharacterConfig Config { get; }
    }
}
