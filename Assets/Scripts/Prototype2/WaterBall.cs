using DeepseaOil.Presentation;
using DeepseaOil.Prototype2;
using UnityEngine;

namespace DeepSeaOil.Prototype2
{
    // 喷泉喷出的水球

    public class WaterBall : MonoBehaviour
    {
        [Header("Parabola")]
        [SerializeField]
        private float flightDuration = 1f;

        [SerializeField]
        private float parabolaHeight = 2f;

        [Header("Move To Player")]
        [SerializeField]
        private float playerMoveSpeed = 8f;

        [SerializeField]
        private float playerReachDistance = 0.05f;

        private Vector2 _startPosition;
        private Vector2 _landingPosition;

        private float _elapsedTime;

        private bool _isFlyingToPlayer;

        private PlayerController _player;

        public void Launch(Vector2 startPosition, Vector2 landingPosition)
        {
            _startPosition = startPosition;
            _landingPosition = landingPosition;

            transform.position = startPosition;

            _elapsedTime = 0f;
            _isFlyingToPlayer = false;
        }

        private void Update()
        {
            if (_isFlyingToPlayer)
            {
                MoveToPlayer();
                return;
            }

            MoveAlongParabola();
        }

        private void MoveAlongParabola()
        {
            _elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(_elapsedTime / flightDuration);

            Vector2 position = Vector2.Lerp(
                _startPosition,
                _landingPosition,
                t
            );

            // 0 -> 1 -> 0
            float height = 4f * parabolaHeight * t * (1f - t);

            position.y += height;

            transform.position = position;

            if (t >= 1f)
            {
                transform.position = _landingPosition;

                // 到达落点
                enabled = true;
            }
        }

        private void MoveToPlayer()
        {
            if (_player == null)
            {
                _isFlyingToPlayer = false;
                return;
            }

            Vector2 currentPosition = transform.position;
            Vector2 targetPosition = _player.transform.position;

            transform.position = Vector2.MoveTowards(
                currentPosition,
                targetPosition,
                playerMoveSpeed * Time.deltaTime
            );

            if (Vector2.Distance(transform.position, targetPosition)
                <= playerReachDistance)
            {
                FindObjectOfType<ThrowSpawner>().AddOneWaterBall();
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (_isFlyingToPlayer)
                return;

            PlayerController player =
                collision.GetComponent<PlayerController>();

            if (player == null)
                return;

            _player = player;
            _isFlyingToPlayer = true;
        }
    }
}