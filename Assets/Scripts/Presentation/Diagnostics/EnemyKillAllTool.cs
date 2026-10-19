#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Presentation.Actor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <summary>关卡测试用秒杀：按 K 灭全场敌人，走真实致死链路（掉落 / 波次结算 / 死亡特效）</summary>
    /// <remarks>账只认 CombatRoot.Enemies；CombatRoot.RenderTick 轮询，关卡场景不必挂组件。场景木桩一起杀（respawnDelay 自活或 Harness 补桩）。</remarks>
    internal static class EnemyKillAllTool
    {
        internal const Key Hotkey = Key.K;

        /// <summary>先快照再打：死亡回调里会改名单</summary>
        private static readonly List<EnemyActor> Targets = new List<EnemyActor>(32);

        private static CombatRoot _announced;

        internal static void Poll(CombatRoot combat)
        {
            if (combat == null || !combat.IsReady) return;

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null) return;

            if (!ReferenceEquals(_announced, combat))
            {
                _announced = combat;

                Debug.Log($"[KillAll] 关卡 {combat.CurrentLevelId}：按 {Hotkey} 秒杀全场敌人。");
            }

            if (!keyboard[Hotkey].wasPressedThisFrame) return;

            int killed = KillAll(combat);

            Debug.Log(killed > 0 ? $"[KillAll] 已秒杀 {killed} 只敌人。" : "[KillAll] 场上没有存活敌人。");
        }

        /// <summary>打死当前所有存活敌人，返回实际死亡数</summary>
        internal static int KillAll(CombatRoot combat)
        {
            if (combat == null) return 0;

            IReadOnlyList<EnemyActor> all = combat.Enemies;

            Targets.Clear();

            for (int i = 0; i < all.Count; i++)
            {
                EnemyActor enemy = all[i];

                if (enemy != null && enemy.IsAlive) Targets.Add(enemy);
            }

            int killed = 0;

            for (int i = 0; i < Targets.Count; i++)
            {
                EnemyActor enemy = Targets[i];

                if (enemy == null || !enemy.IsAlive) continue;

                // 伤害值取当前耐久：飘字即真实血量，打死不多不少
                Damage damage = Damage.At(enemy.Position, enemy.Position, enemy.Hp, DamageSource.Environment);

                enemy.TakeDamage(in damage);

                if (!enemy.IsAlive) killed++;
            }

            Targets.Clear();

            return killed;
        }
    }
}
#endif
