using DeepseaOil.Presentation;
using UnityEngine;

namespace DeepSeaOil.Prototype2
{
    public class Fountain : MonoBehaviour
    {
        [Header("Water Ball")]
        [SerializeField]
        private GameObject waterBallPrefab;

        [SerializeField]
        private float spawnInterval = 1f;

        [Header("Landing")]
        [SerializeField]
        private float landingRadius = 3f;

        [SerializeField]
        private float minLandingDistance = 0.8f;

        [SerializeField]
        private GameObject radiuView;

        private bool _playerInside;
        private float _spawnTimer;

        private void Update()
        {
            if (!_playerInside)
                return;

            _spawnTimer += Time.deltaTime;

            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer -= spawnInterval;

                SpawnWaterBall();
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (collision.GetComponent<PlayerController>() == null)
                return;

            if (_playerInside)
                return;

            _playerInside = true;
            if (_spawnTimer >= spawnInterval)
            {
                SpawnWaterBall();
                _spawnTimer = 0f;
            }

            radiuView.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponent<PlayerController>() == null)
                return;

            _playerInside = false;

            radiuView.SetActive(false);
        }

        private void SpawnWaterBall()
        {
            Vector2 center = transform.position;

            Vector2 offset;

            do
            {
                offset = Random.insideUnitCircle * landingRadius;
            }
            while (offset.magnitude < minLandingDistance);

            Vector2 landingPosition = center + offset;

            GameObject waterBall = Instantiate(
                waterBallPrefab,
                center,
                Quaternion.identity
            );

            waterBall.GetComponent<WaterBall>().Launch(
                center,
                landingPosition
            );
        }
    }
}