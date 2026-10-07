using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个敌人种类的取值边界：合并「<c>enemy</c> 表行」与「按表值造出的 <c>CharacterConfig</c>」。
    /// </summary>
    /// <remarks>
    /// <b>它取代了旧的 <c>EnemySpec</c>（纯结构体）与 <c>EnemyCharacterFactory</c>（折算器）两件。</b>
    /// 旧链是"表行 → EnemySpec → EnemyCharacterFactory → CharacterConfig"，三个类型、
    /// 两处搬运；现在"表 → 角色配置"的翻译只发生在本构造里一处，搬运链不再需要。
    /// <para><b>它不含观感。</b>收口前本类另持一份 <c>VisualPalette</c> 并对外暴露，
    /// 于是调色板多出一个入口、格子高亮还借它取色。现在观感取值的唯一权威入口是
    /// <c>ConfigModule.Visuals</c>，本类只装战斗与运动数值。</para>
    /// <para><b>与敌人无关的项显式清零</b>（冲刺、8 向吸附、外力）：<c>CharacterConfig</c> 的字段带默认值，
    /// 不写就是"继承了玩家那份默认值"，而那种错不会报错，只会在将来某处被读到 ——
    /// 比如某天有人给敌人加了"冲刺"。</para>
    /// <para><b>受击滑停用 <c>knockback_decay</c> 而不是 <c>acceleration</c></b>：这条列的语义正是
    /// "被撞之后速度怎么掉"，用它才能保住"被撞出去多远"的手感。同一列喂 <c>turnDecayRate</c>
    /// 与 <c>hurtDecay</c> 两个尺度不同的字段是刻意的：转身是"反向输入时改向的快慢"，
    /// 滑停是"没有输入时的衰减"，两者在敌人身上的数值恰好同源。</para>
    /// </remarks>
    public sealed class EnemySpec
    {
        private readonly Enemy _row;

        /// <param name="row">表行（<c>enemy</c>）。</param>
        public EnemySpec(Enemy row)
        {
            _row = row;

            // 运行期按种类造一份：表里没有 SO 这一列，而为每只敌人拖一个资产是无意义的劳动。
            // 消费者只有装配链（EnemyLogic → 执行器）；每只敌人各持一份 ⇒ 改一只不影响另一只。
            var config = ScriptableObject.CreateInstance<CharacterConfig>();

            config.name = $"EnemyConfig_{row.Id}";
            config.Name = row.Name;
            config.moveSpeed = row.MaxSpeed;
            config.snapToEightDirections = false;   // 敌人不吃输入，吸附与否无关；显式关掉避免将来误用
            config.moveAcceleration = row.Acceleration;
            config.turnDecayRate = row.KnockbackDecay;
            config.hurtDecay = row.KnockbackDecay;
            config.extraForceScale = 0f;            // 敌人不走外力累加（速度由自己的账本写）
            config.dashSpeed = 0f;                  // 敌人没有冲刺
            config.dashDuration = 0f;

            Config = config;
        }

        /// <summary>编号（= 表主键）。</summary>
        public int Id => _row.Id;

        /// <summary>显示名。</summary>
        public string Name => _row.Name;

        /// <summary>视觉与碰撞半径（世界单位）。</summary>
        public float Radius => _row.Radius;

        /// <summary>追击满速（单位/秒）。</summary>
        public float MaxSpeed => _row.MaxSpeed;

        /// <summary>进入这个距离就不再压上去（世界单位）。</summary>
        public float StopDistance => _row.StopDistance;

        /// <summary>超出这个距离就放弃追击（世界单位）。</summary>
        public float ChaseRange => _row.ChaseRange;

        /// <summary>耐久。</summary>
        public int Hp => _row.Hp;

        /// <summary>受击闪烁频率（Hz）。</summary>
        public float FlashHz => _row.FlashHz;

        /// <summary>
        /// 角色运动配置（由本类在构造时按表值造出）。
        /// </summary>
        /// <remarks>
        /// 消费者是<b>装配链</b>（<c>EnemyLogic</c> 把它交给执行器）—— 状态机读的是执行器折算出的
        /// 六个标量（<c>Foundation.MotionParams</c>），不是本属性。所以它是"配置从哪来"的答案，
        /// 不是状态层的取数口。
        /// </remarks>
        public CharacterConfig Config { get; }
    }
}
