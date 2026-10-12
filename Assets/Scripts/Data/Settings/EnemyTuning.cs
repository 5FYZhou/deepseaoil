using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    // 敌人运动学调参：enemy 表退役物理列的落点
    // 缺行即警告，不逐帧刷屏
    [CreateAssetMenu(fileName = "EnemyTuning", menuName = "DeepseaOil/Tuning/Enemy")]
    public sealed class EnemyTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/EnemyTuning";

        [Header("怪种条目")]
        [Tooltip("每个 enemy.id 一行，参数不共用；查不到落到第一条并警告")]
        public List<EnemyTuningEntry> entries = new List<EnemyTuningEntry>();

        // 未命中缓存：警告只发一次，不逐帧刷屏
        private readonly HashSet<int> _warned = new HashSet<int>();

        // 唯一读取口 ConfigModule.GetEnemy()；id 查不到落到第一条
        public bool TryGet(int id, out EnemyTuningEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    EnemyTuningEntry candidate = entries[i];

                    if (candidate != null && candidate.id == id)
                    {
                        entry = candidate;

                        return true;
                    }
                }
            }

            entry = Fallback(id);

            return false;
        }

        private EnemyTuningEntry Fallback(int id)
        {
            EnemyTuningEntry first = entries != null && entries.Count > 0 ? entries[0] : null;

            if (first == null) first = new EnemyTuningEntry();

            if (_warned.Add(id))
            {
                Debug.LogWarning(
                    $"[Config] EnemyTuning 里没有 enemy.id={id} 的条目，已退回第一条（id={first.id}）。" +
                    $"请在 Assets/Resources/{ResourceKey}.asset 里为每个怪种补一行。", this);
            }

            return first;
        }
    }
}
