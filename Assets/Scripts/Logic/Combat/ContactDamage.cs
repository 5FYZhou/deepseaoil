using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Grid;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>
    /// 接触判定：<b>谁贴在玩家身上</b>。静态纯函数，只吃"玩家在哪一格 ＋ 归属表"。
    /// </summary>
    /// <remarks>
    /// <b>为什么从物理查询改成按格查询：</b>收口前这一条在表现层的
    /// <c>PlayerHealthController.TouchEnemy</c> 里，用 <c>Physics2D.OverlapCircleNonAlloc</c> 找敌人 ——
    /// 那是"只有挂了物理组件才测得了"的判定，和本工程"格为单位、逻辑可测"的取向相反。
    /// <para><b>为什么是九宫格而不是单格：</b>玩家站在自己格子的哪个位置都有可能，
    /// 而"贴着"的判定半径（表值 <c>contact_radius</c>）与格边长同量级 ——
    /// 只看玩家所在格会漏掉"站在格边、敌人正在隔壁格贴着"这一档。
    /// 3×3 邻域覆盖半径 1（格边长 1）的全部候选，所以判定结果与原来的物理圆一致，
    /// 却不再需要物理世界。半径与格边长的比例变了要一起改这里（写在明处）。</para>
    /// <para><b>已死目标不算接触</b>：与 <c>GridLogic.Deal</c> 同一条纪律 ——
    /// 表里可能还留着"已死但没被销毁"的条目。</para>
    /// <para>缓冲由调用方给（与 <see cref="GridQuery"/> 同一条纪律）：这条判定每个物理帧都跑，
    /// 返回新 List 等于每帧产生垃圾。</para>
    /// </remarks>
    public static class ContactDamage
    {
        /// <summary>接触判定扫描的格数（九宫格）。</summary>
        public const int ScannedCells = 9;

        /// <summary>
        /// 找玩家身边最近的接触者。
        /// </summary>
        /// <param name="playerCell">玩家所在格。</param>
        /// <param name="playerPosition">玩家位置（世界坐标）。</param>
        /// <param name="contactRadius">接触半径（世界单位）；非法值按 0（只有重合才算）处理。</param>
        /// <param name="registry">敌人归属表。</param>
        /// <param name="cellBuffer">复用的格缓冲（会先被清空）。</param>
        /// <param name="attacker">最近接触者的位置。</param>
        /// <param name="distance">它到玩家的距离。</param>
        /// <returns>找到了为 <c>true</c>。</returns>
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

            // 中心格自己也要看：GridQuery 的邻居查询按定义不含自己
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

        /// <summary>看一格上的目标，挑出更近的那个接触者。</summary>
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
                // 直接读它的 Position 会抛 MissingReferenceException —— 先按 Unity 的 null 判定剔掉。
                if (target is UnityEngine.Object unityObject && unityObject == null) continue;

                // IsDead 也要先判：已销毁的 MonoBehaviour 一碰 Position 就抛
                if (target == null || target.IsDead) continue;

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
