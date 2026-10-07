using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Drop;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    /// <summary>喷泉：玩家站在触发区里时，按间隔产出一颗水球掉落物。</summary>
    // 由组合根每帧推进（CombatRoot 遍历接线上的喷泉），不是自驱 Update。节拍与落点散布归本类，
    // 掉落物怎么飞、被领走时给什么归掉落物实体与 DropDirector；本类只认识 IDropSpawner 一个方法。
    public sealed class Fountain : MonoBehaviour
    {
        [Header("产出")]
        [Tooltip("产出的间隔（秒）")]
        [SerializeField] private float spawnInterval = 1f;

        [Tooltip("（可选）触发区可视半径提示。玩家进出时显隐")]
        [SerializeField] private GameObject radiusView = default;

        [Header("落点")]
        [Tooltip("落点离喷泉的最大距离（世界单位）")]
        [SerializeField] private float landingRadius = 3f;

        [Tooltip("落点离喷泉的最小距离（世界单位）：避免水球落在喷泉正中心")]
        [SerializeField] private float minLandingDistance = 0.8f;

        // 掉落物生成口；为 null 时不产出。Attach 由组合根调一次。
        private IDropSpawner _spawner;

        private bool _playerInside;
        private float _spawnTimer;

        public void Attach(IDropSpawner spawner)
        {
            _spawner = spawner;
        }

        // 推进一个渲染帧（由组合根驱动）；deltaTime 暂停时为 0，节拍自然冻结。
        public void Tick(float deltaTime)
        {
            if (!_playerInside) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer < spawnInterval) return;

            _spawnTimer -= spawnInterval;

            SpawnOne();
        }

        // 用 Enter 而不是 Stay：函数体只在首次进入那一帧做事，挂在每帧回调上会让人以为节拍在这里推进。
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            if (_playerInside) return;

            _playerInside = true;

            // 进门立刻给一颗（若已经攒够一个间隔）：站一下就走也应该拿到东西。
            if (_spawnTimer >= spawnInterval)
            {
                SpawnOne();
                _spawnTimer = 0f;
            }

            if (radiusView != null) radiusView.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            _playerInside = false;

            if (radiusView != null) radiusView.SetActive(false);
        }

        // 没接线时不产出、也不刷错误：那是装配问题，CombatRoot 的装配日志已经说过一次。
        private void SpawnOne()
        {
            if (_spawner == null) return;

            Vector2 center = transform.position;
            Vector2 landing = center + RandomLandingOffset();

            _spawner.TrySpawn(new DropSpawnRequest(DropType.Water, center, landing));
        }

        // 单位圆内随机 × 半径，滤掉太靠近中心的那一圈。
        // 最多试 MaxLandingAttempts 次：最小距离大于最大半径时理论上永远试不出来，
        // 次数上限把"配置写错"变成"落点近一点"，而不是死循环。
        private Vector2 RandomLandingOffset()
        {
            const int MaxLandingAttempts = 8;

            float radius = Mathf.Max(0f, landingRadius);
            float min = Mathf.Max(0f, minLandingDistance);

            for (int i = 0; i < MaxLandingAttempts; i++)
            {
                Vector2 offset = Rng.InsideUnitCircle() * radius;

                if (offset.magnitude >= min || radius <= min) return offset;
            }

            return Rng.InsideUnitCircle() * radius;
        }
    }
}
