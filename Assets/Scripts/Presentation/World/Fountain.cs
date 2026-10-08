using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Drop;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    // 由组合根每帧推进，不是自驱 Update；只认识 IDropSpawner 一个方法
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

        // 为 null 时不产出；Attach 由组合根调一次
        private IDropSpawner _spawner;

        private bool _playerInside;
        private float _spawnTimer;

        public void Attach(IDropSpawner spawner)
        {
            _spawner = spawner;
        }

        // 推进一个渲染帧；deltaTime 暂停时为 0，节拍自然冻结
        public void Tick(float deltaTime)
        {
            if (!_playerInside) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer < spawnInterval) return;

            _spawnTimer -= spawnInterval;

            SpawnOne();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            if (_playerInside) return;

            _playerInside = true;

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

        // 没接线时不产出也不报错，装配日志已由 CombatRoot 报过
        private void SpawnOne()
        {
            if (_spawner == null) return;

            Vector2 center = transform.position;
            Vector2 landing = center + RandomLandingOffset();

            _spawner.TrySpawn(new DropSpawnRequest(DropType.Water, center, landing));
        }

        // 上限 MaxLandingAttempts 次，避免最小距离>最大半径时死循环
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
