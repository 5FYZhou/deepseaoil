using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Grid;

namespace DeepseaOil.Logic.Combat
{
    // 接触检测的静态纯函数：谁贴在玩家身上（最近者的位置与距离）。它不产生伤害，
    // Damage 由调用方自己造，伤害值取自 PlayerSpec.ContactDamage。
    // 按九宫格查而不是物理查询：3×3 邻域覆盖半径 1（格边长 1）的全部候选，判定与原物理圆一致；
    // 但"贴着"的判定半径（表值 contact_radius）与格边长的比例变了，要一起改这里。
    // 已死目标不算接触：与 GridLogic.Deal 同一条纪律，表里可能还留着"已死但没被销毁"的条目。
    // 缓冲由调用方给（与 GridQuery 同一条纪律）：每个物理帧都跑，返回新 List 等于每帧产生垃圾。
    public static class ContactProbe
    {
        // 接触判定扫描的格数（九宫格）。
        public const int ScannedCells = 9;

        // cellBuffer 是复用的格缓冲（会先被清空）。
        // contactRadius 为世界单位；非法值按 0（只有重合才算）处理。
        public static bool TryFindAttacker(
            Vector3Int playerCell,
            Vector2 playerPosition,
            float contactRadius,
            EnemyCellRegistry registry,
            List<Vector3Int> cellBuffer,
            out Vector2 attacker,
            out float distance)
        {
            attacker = default;
            distance = float.PositiveInfinity;

            if (registry == null || cellBuffer == null) return false;

            float radius = float.IsNaN(contactRadius) || contactRadius < 0f ? 0f : contactRadius;
            float radiusSqr = radius * radius;

            bool found = false;
            float bestSqr = float.MaxValue;

            // GridQuery 的邻居查询按定义不含自己，中心格要单独看。
            found = ScanCell(playerCell, playerPosition, radiusSqr, registry, ref bestSqr, ref attacker);

            GridQuery.GetNeighbors8(playerCell, cellBuffer);

            for (int i = 0; i < cellBuffer.Count; i++)
            {
                if (ScanCell(cellBuffer[i], playerPosition, radiusSqr, registry, ref bestSqr, ref attacker)) found = true;
            }

            if (!found) return false;

            distance = Mathf.Sqrt(bestSqr);

            return true;
        }

        private static bool ScanCell(
            Vector3Int cell,
            Vector2 playerPosition,
            float radiusSqr,
            EnemyCellRegistry registry,
            ref float bestSqr,
            ref Vector2 attacker)
        {
            if (!registry.TryGetIn(cell, out List<IDamageable> targets) || targets == null) return false;

            bool found = false;

            for (int i = 0; i < targets.Count; i++)
            {
                IDamageable target = targets[i];

                // 已销毁的 Unity 对象在**接口引用**上不是 null（Unity 的 == 重载不参与接口比较），
                // 直接读它等于抛 MissingReferenceException —— 先按 Unity 的 null 判定剔掉。
                if (target is UnityEngine.Object unityObject && unityObject == null) continue;

                // 已死但未被销毁的目标不该继续算接触（生命体征走 IAlivable，玩家侧与怪物侧同名）。
                if (target == null) continue;
                if (target is IAlivable livable && !livable.IsAlive) continue;

                Vector2 delta = playerPosition - target.Position;
                float sqr = delta.sqrMagnitude;

                if (sqr > radiusSqr) continue;

                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                attacker = target.Position;
                found = true;
            }

            return found;
        }
    }
}
