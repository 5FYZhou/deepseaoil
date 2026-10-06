using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 把 <see cref="EnemySpec"/> 折算成 <c>ActorLogic</c> 要求的 <see cref="CharacterConfig"/>。
    /// </summary>
    /// <remarks>
    /// <b>为什么不复用玩家那份 SO 资产：</b>两者要的是不同的角色行为（玩家零惯性、敌人有加减速），
    /// 共用一份配置只会让"改一个数影响另一个"。表里也没有 SO 这一列 ——
    /// 与其为每只敌人拖一个资产，不如运行期按种类造一份。
    /// <para><b>按<b>种类</b>缓存而不是逐只新建：</b>白模逐只建一份的理由是"共享实例有隐蔽耦合"，
    /// 而 <see cref="CharacterConfig"/> 的消费者（<c>ActorLogic</c>）只读不写，耦合面为零；
    /// 逐只新建则是每刷一只敌人泄漏一个 SO。缓存键是敌人编号，全场最多几个对象。</para>
    /// <para><b>与敌人无关的项显式清零</b>（冲刺、8 向吸附、外力）：<c>CharacterConfig</c> 的字段带默认值，
    /// 不写就是"继承了玩家那份默认值"，而那种错不会报错，只会在将来某处被读到 ——
    /// 比如某天有人给敌人加了"冲刺"。</para>
    /// </remarks>
    public static class EnemyCharacterFactory
    {
        private static readonly Dictionary<int, CharacterConfig> Cache = new();

        /// <summary>取（或造）某个敌人数值对应的角色配置。</summary>
        public static CharacterConfig Build(in EnemySpec spec)
        {
            if (Cache.TryGetValue(spec.Id, out CharacterConfig cached) && cached != null) return cached;

            var config = ScriptableObject.CreateInstance<CharacterConfig>();

            config.name = $"敌人配置_{spec.Id}_{spec.Name}";

            config.Name = spec.Name;
            config.moveSpeed = spec.MaxSpeed;
            config.snapToEightDirections = false;   // 敌人不吃输入，吸附与否无关；显式关掉避免将来误用
            config.moveAcceleration = spec.Acceleration;
            config.turnDecayRate = spec.KnockbackDecay;

            // 受击滑停用 knockback_decay 而不是 acceleration：这条列的语义正是"被撞之后速度怎么掉"，
            // 用它才能保住"被撞出去多远"的手感（冲量 5.5 / 10 ⇒ 约 1.5 米，与禁足时代同量级）。
            config.hurtDecay = spec.KnockbackDecay;
            config.extraForceScale = 0f;            // 敌人不走 ApplyExtraForce（速度由自己的账本写）
            config.dashSpeed = 0f;                  // 敌人没有冲刺
            config.dashDuration = 0f;

            Cache[spec.Id] = config;

            return config;
        }

        /// <summary>丢掉缓存（切场景 / 重新读表时用）。</summary>
        /// <remarks>不销毁实例：它可能仍被活着的敌人持有，销毁会让它们下一次读配置时抛。</remarks>
        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
