using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    /// <summary>
    /// 喷泉喷出的水球：先走一段抛物线到落点，等玩家碰到它，再飞向玩家并<b>发一条事实事件</b>。
    /// </summary>
    /// <remarks>
    /// <b>领取走事件而不是找组合根：</b>白模里这一句是
    /// <c>FindObjectOfType&lt;ThrowSpawner&gt;().AddOneWaterBall()</c> —— 一个反模式：
    /// 它让"谁加水球"这件事只能靠搜代码回答，而且在水球被销毁/组合根缺失时静默失效。
    /// 现在它只发 <see cref="WaterBallCollected"/>，订阅方（组合根）决定加多少。
    /// <para><b>由喷泉每帧推进，不是自驱 <c>Update</c></b>：与球、敌人同一条纪律 ——
    /// 每帧的驱动入口只有组合根那几个。</para>
    /// </remarks>
    public sealed class WaterBall : MonoBehaviour
    {
        [Header("抛物线")]
        [Tooltip("从喷泉飞到落点的时长（秒）")]
        [SerializeField] private float flightDuration = 1f;

        [Tooltip("抛物线弧高（世界单位）")]
        [SerializeField] private float parabolaHeight = 2f;

        [Header("飞向玩家")]
        [Tooltip("落点→玩家的飞行速度（单位/秒）")]
        [SerializeField] private float playerMoveSpeed = 8f;

        [Tooltip("判定「够到了玩家」的距离（世界单位）")]
        [SerializeField] private float playerReachDistance = 0.05f;

        private Vector2 _startPosition;
        private Vector2 _landingPosition;

        private float _elapsedTime;

        private bool _flyingToPlayer;
        private bool _collected;

        private Transform _player;

        /// <summary>是否已经飞到玩家身上（已被领取）。</summary>
        public bool IsCollected => _collected;

        /// <summary>
        /// 起飞。落点由喷泉按随机方向算好传进来。
        /// </summary>
        public void Initialize(Vector2 startPosition, Vector2 landingPosition)
        {
            _startPosition = startPosition;
            _landingPosition = landingPosition;

            transform.position = startPosition;

            _elapsedTime = 0f;
            _flyingToPlayer = false;
            _collected = false;
        }

        /// <summary>推进一个渲染帧（由喷泉驱动）。</summary>
        public void Tick(float deltaTime)
        {
            if (_collected) return;

            if (_flyingToPlayer)
            {
                MoveToPlayer(deltaTime);
                return;
            }

            MoveAlongParabola(deltaTime);
        }

        private void MoveAlongParabola(float deltaTime)
        {
            // 时长为 0 时直接落到落点，不除出非数。
            float t = flightDuration > 0f ? Mathf.Clamp01(_elapsedTime / flightDuration) : 1f;

            _elapsedTime += deltaTime;

            Vector2 position = Vector2.Lerp(_startPosition, _landingPosition, t);

            // 0 → 1 → 0：两端恰好为 0，所以"落地"那一刻高度精确归零。
            position.y += 4f * parabolaHeight * t * (1f - t);

            transform.position = position;

            if (t >= 1f) transform.position = _landingPosition;
        }

        private void MoveToPlayer(float deltaTime)
        {
            if (_player == null)
            {
                // 玩家不见了（切场景 / 被销毁）：回到"等触发"状态，不销毁自己。
                _flyingToPlayer = false;
                return;
            }

            Vector2 currentPosition = transform.position;
            var targetPosition = new Vector2(_player.position.x, _player.position.y);

            Vector2 next = Vector2.MoveTowards(currentPosition, targetPosition, playerMoveSpeed * deltaTime);

            transform.position = next;

            if (Vector2.Distance(next, targetPosition) > playerReachDistance) return;

            _collected = true;

            EventBus<WaterBallCollected>.Publish(new WaterBallCollected());

            Destroy(gameObject);
        }

        /// <summary>玩家碰到水球（水球是触发器，玩家有刚体 ⇒ 触发回调会到两边）。</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected || _flyingToPlayer) return;

            // 只认玩家：认 <c>PlayerController</c> 而不是某种 Tag —— Tag 是一处需要人工同步的工程设置。
            if (other.GetComponentInParent<PlayerController>() == null) return;

            _player = other.transform;
            _flyingToPlayer = true;
        }
    }
}
