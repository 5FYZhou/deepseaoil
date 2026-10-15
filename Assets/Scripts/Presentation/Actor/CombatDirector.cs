using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Level;
using DeepseaOil.Logic.Wave;
using UnityEngine;

namespace DeepseaOil.Presentation.Actor
{
        // 战斗调度：按波次三阶段生成与清场
        // 波次与存活数的唯一权威；预制体与 enemy 表缺件是硬错误
    public sealed class CombatDirector : MonoBehaviour
    {
        // 出生点抽样上限，超了接受当前点：相机跟随会让刚采的点又进屏
        private const int MaxSpawnTries = 8;

        private CombatRoot _combatRoot;
        private LevelLogic _levelLogic;
        private Transform _player;
        private Transform _actorRoot;

        // 怪种取值口，组合根注入
        private System.Func<int, EnemySpec> _specOf;

        private System.Action<EnemySpec, Vector2> _onKilled;

        [Tooltip("用来算屏幕外出生点的相机。留空则在装配时回落 Camera.main。")]
        [SerializeField] private Camera spawnCamera = default;

        [Tooltip("出生点离可见区边缘再往外推的距离（世界单位）")]
        [SerializeField] private float spawnScreenMargin = 0.5f;

        [Tooltip("出生环上界 = (可见区外接半径 + margin) × 本系数；小于 1 会被抬成 1")]
        [SerializeField] private float spawnRadiusMultiplier = 1.4f;

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();
        private readonly List<WaveLogic.SpawnRequest> _spawnBuffer = new List<WaveLogic.SpawnRequest>();

        private BoundsArea _bounds;

        private int _publishedWave = -1;
        private int _publishedAlive = -1;

        public int AliveCount { get; private set; }

        public WaveLogic Logic => _levelLogic?.Waves;

        public float FirstAliveEnemyDistance { get; private set; } = -1f;

        public Vector2 FirstAliveEnemyVelocity { get; private set; }

        public void Initialize(
            CombatRoot combatRoot,
            PlayerController player,
            LevelLogic levelLogic,
            System.Func<int, EnemySpec> specOf,
            Transform actorRoot,
            Camera camera,
            in BoundsArea bounds,
            System.Action<EnemySpec, Vector2> onKilled = null)
        {
            if (combatRoot == null || player == null || player.Logic == null || levelLogic == null)
            {
                Debug.LogError("CombatDirector 缺少战斗根、玩家逻辑或关卡逻辑，敌人不会生成，已停用。", this);
                enabled = false;
                return;
            }

            if (specOf == null)
            {
                Debug.LogError("CombatDirector 没有怪种取值口，敌人不会生成，已停用。", this);
                enabled = false;
                return;
            }

            _combatRoot = combatRoot;
            _levelLogic = levelLogic;
            _player = player.transform;
            _specOf = specOf;
            _actorRoot = actorRoot;
            _bounds = bounds;
            _onKilled = onKilled;

            ResolveSpawnCamera(camera);

            PublishIfChanged();
        }

        public void ClearAll()
        {
            // 销毁权归 CombatRoot 的权属分流，本类只清自己的账
            _enemies.Clear();

            AliveCount = 0;
            FirstAliveEnemyDistance = -1f;
            FirstAliveEnemyVelocity = Vector2.zero;

            _publishedWave = -1;
            _publishedAlive = -1;

            PublishIfChanged();

            Debug.Log("[Combat] 敌人清场，等待下一波");
        }

        // 暂停时 deltaTime 为 0；恢复那帧不补欠的生成量
        public void FixedTick(float now, float deltaTime)
        {
            if (_levelLogic == null || _player == null) return;

            ClearDestroyed();

            AliveCount = CountAlive();
            UpdateReadouts();

            _levelLogic.Tick(deltaTime, AliveCount > 0, _spawnBuffer);

            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                SpawnOne(_spawnBuffer[i]);
            }

            PublishIfChanged();
        }

        private void SpawnOne(in WaveLogic.SpawnRequest request)
        {
            EnemySpec spec = _specOf(request.EnemyId);

            if (spec == null)
            {
                Debug.LogError(
                    $"[Combat] enemy 表里没有 id={request.EnemyId}（本波 spawn 请求已取消）。" +
                    "wave_enemy.enemy_id 与 enemy.id 必须逐项对得上。");

                return;
            }

            string prefabKey = $"enemies/Enemy_{request.EnemyId}";

            GameObject prefab = AssetModule.IsInitialized ? AssetModule.Load<GameObject>(prefabKey) : null;

            if (prefab == null)
            {
                Debug.LogError($"[Combat] 严重阻断：未找到敌人预制体 Assets/Resources/{prefabKey}.prefab！本次生成已取消。请先在 Unity 中创建该预制体。");
                return;
            }

            Vector2 position = SpawnPosition();

            GameObject go = Instantiate(prefab, position, Quaternion.identity, _actorRoot);
            go.name = $"Enemy_{request.WaveIndex}_{request.Remaining}";

            EnemyActor actor = go.GetComponent<EnemyActor>();

            if (actor == null)
            {
                Debug.LogError($"[Combat] 预制体 {prefabKey} 根节点未挂载 EnemyActor 组件！生成已作废。");
                Destroy(go);
                return;
            }

            if (_onKilled != null) actor.Died = OnEnemyDied;

            _combatRoot.AdoptWaveEnemy(actor, position, in spec, PlayerPosition() - position);

            _enemies.Add(actor);
        }

        // 屏幕外出生点：可见区外接半径起算的外环上按角度取点
        // 每只现采一次，不跨帧缓存相机位置（相机由 Cinemachine 带）
        private Vector2 SpawnPosition()
        {
            Vector2 center;

            float halfWidth;
            float halfHeight;

            if (!TryReadView(out center, out halfWidth, out halfHeight)) return PlayerPosition();

            float inner = Mathf.Max(halfWidth, halfHeight) + Mathf.Max(0f, spawnScreenMargin);
            float multiplier = spawnRadiusMultiplier > 1f ? spawnRadiusMultiplier : 1f;

            Vector2 candidate = center;

            for (int tryIndex = 0; tryIndex < MaxSpawnTries; tryIndex++)
            {
                float angle = Rng.Value01() * 2f * Mathf.PI;

                float distance = Mathf.Lerp(inner, inner * multiplier, Rng.Value01());

                candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                if (!IsInsideView(candidate, center, halfWidth, halfHeight)) break;
            }

            // 边界优先于"绝对不可见"：钳回后落进屏内也接受
            if (_bounds.TryClamp(candidate, out Vector2 clamped)) candidate = clamped;

            return candidate;
        }

        // 正交可见区中心与半尺寸；相机缺失返回 false
        private bool TryReadView(out Vector2 center, out float halfWidth, out float halfHeight)
        {
            if (spawnCamera == null)
            {
                center = Vector2.zero;
                halfWidth = 0f;
                halfHeight = 0f;

                return false;
            }

            halfHeight = Mathf.Abs(spawnCamera.orthographicSize);
            halfWidth = halfHeight * spawnCamera.aspect;
            center = spawnCamera.transform.position;

            return true;
        }

        private bool IsInsideView(Vector2 position, Vector2 center, float halfWidth, float halfHeight)
        {
            float margin = Mathf.Max(0f, spawnScreenMargin);

            return Mathf.Abs(position.x - center.x) <= halfWidth + margin
                && Mathf.Abs(position.y - center.y) <= halfHeight + margin;
        }

        // 相机缺失只报一条错并退化成默认，不停摆
        private void ResolveSpawnCamera(Camera fallback)
        {
            if (spawnCamera == null) spawnCamera = fallback;

            if (spawnCamera == null) spawnCamera = Camera.main;

            if (spawnCamera == null)
            {
                Debug.LogError(
                    "[Combat] 没有可用相机（spawnCamera 未接线且找不到 MainCamera）：出生点已退化成静态默认值。", this);
            }
        }

        private void OnEnemyDied(EnemyActor enemy)
        {
            EnemySpec spec = enemy != null && enemy.Stats != null ? enemy.Stats.Spec : null;

            if (spec != null) _onKilled?.Invoke(spec, enemy.Position);
        }

        private void ClearDestroyed()
        {
            // 倒序删：正序会跳过下一个元素
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] == null) _enemies.RemoveAt(i);
            }
        }

        private int CountAlive()
        {
            int alive = 0;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy != null && enemy.IsAlive) alive++;
            }

            return alive;
        }

        private void UpdateReadouts()
        {
            FirstAliveEnemyDistance = -1f;
            FirstAliveEnemyVelocity = Vector2.zero;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || !enemy.IsAlive) continue;

                FirstAliveEnemyDistance = Vector2.Distance(enemy.Position, PlayerPosition());
                FirstAliveEnemyVelocity = enemy.EngineVelocity;

                return;
            }
        }

        private void PublishIfChanged()
        {
            int wave = _levelLogic != null ? _levelLogic.Waves.WaveIndex : 0;

            if (wave == _publishedWave && AliveCount == _publishedAlive) return;

            _publishedWave = wave;
            _publishedAlive = AliveCount;

            EventBus<WaveChanged>.Publish(new WaveChanged(wave, AliveCount));
        }

        public void Announce()
        {
            _publishedWave = -1;
            _publishedAlive = -1;

            PublishIfChanged();
        }

        private Vector2 PlayerPosition()
        {
            Vector3 p = _player.position;

            return new Vector2(p.x, p.y);
        }
    }
}
