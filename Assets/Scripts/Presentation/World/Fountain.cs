using System.Collections.Generic;
using DeepseaOil.Logic.Random;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    /// <summary>
    /// 喷泉：玩家站在触发区里时，按间隔喷出水球。
    /// </summary>
    /// <remarks>
    /// <b>由组合根每帧推进</b>（<c>CombatRoot</c> 遍历它接线上的喷泉），不是自驱 <c>Update</c>：
    /// 与球、敌人同一条纪律。
    /// <para><b>水球默认运行期建出</b>（<c>new GameObject</c> ＋ 组件）：于是本组件零必需接线，
    /// 忘了拖预制体也不会静默不工作。挂了预制体则用预制体（换成正式美术时走这条）。</para>
    /// <para><b>随机落点走 <c>Rng</c></b> 而不是 <c>UnityEngine.Random</c>：后者是全局静态状态，
    /// 测试之间会互相污染，且"这一局的随机序列"无法复现。</para>
    /// </remarks>
    public sealed class Fountain : MonoBehaviour
    {
        [Header("水球")]
        [Tooltip("喷出水球的间隔（秒）")]
        [SerializeField] private float spawnInterval = 1f;

        [Tooltip("（可选）水球预制体。留空则运行期建一个纯色圆点水球")]
        [SerializeField] private WaterBall waterBallPrefab = default;

        [Tooltip("（可选）触发区可视半径提示。玩家进出时显隐")]
        [SerializeField] private GameObject radiusView = default;

        [Header("落点")]
        [Tooltip("落点离喷泉的最大距离（世界单位）")]
        [SerializeField] private float landingRadius = 3f;

        [Tooltip("落点离喷泉的最小距离（世界单位）：避免水球落在喷泉正中心")]
        [SerializeField] private float minLandingDistance = 0.8f;

        private readonly List<WaterBall> _balls = new List<WaterBall>();

        private bool _playerInside;
        private float _spawnTimer;

        /// <summary>在场的水球数（诊断用）。</summary>
        public int AliveBallCount => _balls.Count;

        /// <summary>推进一个渲染帧（由组合根驱动）。</summary>
        public void Tick(float deltaTime)
        {
            TickBalls(deltaTime);

            if (!_playerInside) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer < spawnInterval) return;

            _spawnTimer -= spawnInterval;

            SpawnWaterBall();
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            if (!_playerInside)
            {
                _playerInside = true;

                // 进门立刻给一颗，而不是"再等一个间隔"：站一下就走也应该拿到东西。
                if (_spawnTimer >= spawnInterval)
                {
                    SpawnWaterBall();
                    _spawnTimer = 0f;
                }

                if (radiusView != null) radiusView.SetActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            _playerInside = false;

            if (radiusView != null) radiusView.SetActive(false);
        }

        private void TickBalls(float deltaTime)
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                WaterBall ball = _balls[i];

                if (ball == null)
                {
                    _balls.RemoveAt(i);
                    continue;
                }

                ball.Tick(deltaTime);
            }
        }

        private void SpawnWaterBall()
        {
            Vector2 center = transform.position;

            Vector2 landingPosition = center + RandomLandingOffset();

            WaterBall ball = waterBallPrefab != null
                ? Instantiate(waterBallPrefab, center, Quaternion.identity)
                : CreateRuntimeBall(center);

            if (ball == null)
            {
                Debug.LogError("Fountain 的水球预制体上找不到 WaterBall 组件，本次不喷。", this);
                return;
            }

            ball.Initialize(center, landingPosition);

            _balls.Add(ball);
        }

        /// <summary>
        /// 落点偏移：单位圆内随机 × 半径，但滤掉太靠近喷泉中心的那一圈。
        /// </summary>
        /// <remarks>
        /// 最多试 <see cref="MaxLandingAttempts"/> 次：<c>minLandingDistance</c> 大于
        /// <c>landingRadius</c> 时理论上永远试不出来，用次数上限把"配置写错"变成
        /// "落点离得近了点"，而不是一个死循环。
        /// </remarks>
        private Vector2 RandomLandingOffset()
        {
            const int MaxLandingAttempts = 8;

            float min = Mathf.Max(0f, minLandingDistance);

            for (int i = 0; i < MaxLandingAttempts; i++)
            {
                Vector2 offset = Rng.InsideUnitCircle() * Mathf.Max(0f, landingRadius);

                if (offset.magnitude >= min || landingRadius <= min) return offset;
            }

            return Rng.InsideUnitCircle() * Mathf.Max(0f, landingRadius);
        }

        /// <summary>
        /// 运行期建一个水球：纯色圆点 ＋ 触发圆碰撞体。
        /// </summary>
        /// <remarks>
        /// 触发体只需要挂在一边（玩家有刚体），所以水球自己不需要 <c>Rigidbody2D</c>。
        /// </remarks>
        private WaterBall CreateRuntimeBall(Vector2 position)
        {
            var go = new GameObject("水球");

            go.layer = RenderOrder.OverlayLayer;
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();

            PrimitiveSprites.Configure(
                renderer,
                PrimitiveSprites.Circle,
                CombatPalette.WaterBall,
                RenderOrder.Ball,
                0.3f);

            var collider = go.AddComponent<CircleCollider2D>();

            collider.isTrigger = true;
            collider.radius = 0.15f;

            return go.AddComponent<WaterBall>();
        }
    }
}
