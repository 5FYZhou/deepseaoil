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

        [Tooltip("净空皮厚（世界单位）：净空 > 体半径 + 皮厚才可通行。调大更保守（更容易在窄口贴墙停）")]
        public float navSkin = 0.05f;

        [Tooltip("净空烘焙解析上限（世界单位）：查询盒半边长与净空饱和值都是它，必须大于最大体半径")]
        public float navClearanceCap = 1.0f;

        [Range(0.5f, 1f)]
        [Tooltip("元素格步价倍率：1=关闭偏好；0.85=每步便宜15%，约允许1-2格绕路")]
        public float elementTileCost = 0.85f;

        [Tooltip("入弯最低速度倍率：路线方向与当前速度对齐度为 0 时的目标速度倍率。0.4 把 90° 转弯半径收到约 0.4 格")]
        public float cornerSpeedFactor = 0.4f;

        [Tooltip("软分离生效距离（世界单位）：近了互相让开，避免叠在同一个位置")]
        public float separationRadius = 0.45f;

        [Tooltip("软分离权重：与流场方向相加后归一。0 = 不分离，过大 = 挤开流场方向绕远")]
        public float separationWeight = 0.25f;

        [Tooltip("停住时的散开速度（世界单位/秒）：被同伴挤住时沿切向挪开；0 或未填按 1.0")]
        public float spreadSpeed = 1f;

        [Tooltip("卡死判定帧数：速度被压到零满这么多物理帧才算卡死")]
        public int stuckFrames = 18;

        [Tooltip("卡死换步持续帧数：这段时间走次优邻格")]
        public int jamFrames = 6;

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
                turnRateLimit = Pick(entry.turnRateLimit, def.turnRateLimit, first.turnRateLimit),
                turnSmoothSeconds = Pick(entry.turnSmoothSeconds, def.turnSmoothSeconds, first.turnSmoothSeconds),
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
