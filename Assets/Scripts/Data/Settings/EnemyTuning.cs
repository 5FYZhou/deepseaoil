using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data.Settings
{
    // 敌人运动学调参：enemy 表退役物理列的落点
    [CreateAssetMenu(fileName = "EnemyTuning", menuName = "DeepseaOil/Tuning/Enemy")]
    public sealed class EnemyTuning : ScriptableObject
    {
        public const string ResourceKey = "tuning/EnemyTuning";

        [Header("怪种条目")]
        [Tooltip("每个 enemy.id 一行，参数不共用；查不到落到第一条并警告")]
        public List<EnemyTuningEntry> entries = new List<EnemyTuningEntry>();

        [Tooltip("默认条目：entries 里填 0 的字段都从它取；它也填 0 时再落 entries 第一条")]
        public EnemyTuningEntry defaultEntry = new EnemyTuningEntry
        {
            visionRange = 10f,
            sustainRange = 14f,
            loseDelay = 3f,
        };

        [Tooltip("绕障 BFS 深度上限（格）；<=0 即不绕障，敌人退回直线撞墙")]
        public int pathMaxSteps = 16;

        [Tooltip("绕障方向重算间隔（物理帧）；实际间隔比它多 1 帧")]
        public int pathRecalcFrames = 5;

        // 条目填 0 的字段逐级回落（→defaultEntry→第一条）
        private Dictionary<int, EnemyTuningEntry> _merged;

        private readonly HashSet<int> _warned = new HashSet<int>();

        public bool TryGet(int id, out EnemyTuningEntry entry)
        {
            EnsureMerged();

            if (_merged.TryGetValue(id, out entry)) return true;

            entry = Fallback(id);

            return false;
        }

        private void OnValidate()
        {
            _merged = null;
        }

        private void EnsureMerged()
        {
            if (_merged != null) return;

            _merged = new Dictionary<int, EnemyTuningEntry>();

            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                EnemyTuningEntry raw = entries[i];

                if (raw == null || _merged.ContainsKey(raw.id)) continue;

                _merged[raw.id] = Merge(raw);
            }
        }

        private EnemyTuningEntry Merge(EnemyTuningEntry entry)
        {
            EnemyTuningEntry def = defaultEntry != null ? defaultEntry : entry;

            EnemyTuningEntry first = entries.Count > 0 && entries[0] != null ? entries[0] : def;

            return new EnemyTuningEntry
            {
                id = entry.id,
                radius = Pick(entry.radius, def.radius, first.radius),
                acceleration = Pick(entry.acceleration, def.acceleration, first.acceleration),
                knockbackDecay = Pick(entry.knockbackDecay, def.knockbackDecay, first.knockbackDecay),
                stopDistance = Pick(entry.stopDistance, def.stopDistance, first.stopDistance),
                chaseRange = Pick(entry.chaseRange, def.chaseRange, first.chaseRange),
                visionRange = Pick(entry.visionRange, def.visionRange, first.visionRange),
                sustainRange = Pick(entry.sustainRange, def.sustainRange, first.sustainRange),
                loseDelay = Pick(entry.loseDelay, def.loseDelay, first.loseDelay),
                squash = entry.squash != null ? entry.squash : def.squash,
            };
        }

        private static float Pick(float own, float def, float last)
        {
            if (own > 0f) return own;

            return def > 0f ? def : last;
        }

        private EnemyTuningEntry Fallback(int id)
        {
            EnemyTuningEntry first = entries != null && entries.Count > 0 ? entries[0] : null;

            // 落到第一条也要合并，否则填 0 的字段把 0 交出去
            EnemyTuningEntry entry = first != null
                ? (_merged.TryGetValue(first.id, out EnemyTuningEntry cached) ? cached : Merge(first))
                : Merge(new EnemyTuningEntry { id = id });

            if (_warned.Add(id))
            {
                Debug.LogWarning(
                    $"[Config] EnemyTuning 里没有 enemy.id={id} 的条目，已退回第一条（id={entry.id}）。" +
                    $"请在 Assets/Resources/{ResourceKey}.asset 里为每个怪种补一行。", this);
            }

            return entry;
        }
    }
}
