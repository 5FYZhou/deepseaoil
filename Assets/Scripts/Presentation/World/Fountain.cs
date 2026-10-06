using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Random;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    /// <summary>
    /// 喷泉：玩家站在触发区里时，按间隔产出一颗水球掉落物。
    /// </summary>
    /// <remarks>
    /// <b>由组合根每帧推进</b>（<c>CombatRoot</c> 遍历它接线上的喷泉），不是自驱 <c>Update</c>：
    /// 与球、敌人、掉落物同一条纪律。
    /// <para><b>产出三问（产什么 / 怎么产 / 何时产）全归本类</b>（审查已定）：节拍在这里、
    /// 落点散布也在这里。而"掉落物怎么飞、长什么样、被领走时给什么"归掉落物实体与取值定义；
    /// 本类<b>不持有、也不驱动</b>掉落物 —— 那三件事归 <c>DropDirector</c>。</para>
    /// <para><b>随机落点走 <c>Rng</c></b> 而不是 <c>UnityEngine.Random</c>：后者是全局静态状态，
    /// 测试之间会互相污染，且"这一局的随机序列"无法复现。</para>
    /// </remarks>
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

        /// <summary>掉落物生成口（装配期由组合根注入）。为 <c>null</c> 时不产出。</summary>
        private IDropSpawner _spawner;

        private bool _playerInside;
        private float _spawnTimer;

        /// <summary>
        /// 装配：注入掉落物生成口。<b>由组合根调一次</b> —— 本类不认识 <c>DropDirector</c> 本身，
        /// 只认识"能产出一颗掉落物"这一个方法。
        /// </summary>
        /// <param name="spawner">生成口；为 <c>null</c> 时本喷泉不产出。</param>
        public void Attach(IDropSpawner spawner)
        {
            _spawner = spawner;
        }

        /// <summary>推进一个渲染帧（由组合根驱动）。</summary>
        /// <param name="deltaTime">本帧时长；暂停时为 0，节拍自然冻结。</param>
        public void Tick(float deltaTime)
        {
            if (!_playerInside) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer < spawnInterval) return;

            _spawnTimer -= spawnInterval;

            SpawnOne();
        }

        /// <summary>
        /// 玩家进入触发区。
        /// </summary>
        /// <remarks>
        /// <b>用 Enter 而不是 Stay：</b>收口前挂在 <c>OnTriggerStay2D</c> 上，而函数体只在"首次进入"
        /// 那一帧做事 —— 名字与行为不符（Stay 的每帧回调能力完全没用上），读代码的人会以为
        /// 节拍在这里推进。现在名字说的就是它做的事。
        /// <para>进门<b>立刻给一颗</b>（若已经攒够一个间隔）：站一下就走也应该拿到东西。</para>
        /// </remarks>
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

        /// <summary>
        /// 产出一次：算好落点，交给生成口。
        /// </summary>
        /// <remarks>
        /// <b>没接线时不产出、也不刷错误</b>：那是装配问题，<c>CombatRoot</c> 的装配日志已经说过一次 ——
        /// 每帧再刷一条只会把 Console 淹掉。
        /// </remarks>
        private void SpawnOne()
        {
            if (_spawner == null) return;

            Vector2 center = transform.position;
            Vector2 landing = center + RandomLandingOffset();

            _spawner.TrySpawn(new DropSpawnRequest(DropType.Water, center, landing));
        }

        /// <summary>
        /// 落点偏移：单位圆内随机 × 半径，但滤掉太靠近喷泉中心的那一圈。
        /// </summary>
        /// <remarks>
        /// 最多试 <c>MaxLandingAttempts</c> 次：最小距离大于最大半径时理论上永远试不出来，
        /// 用次数上限把"配置写错"变成"落点离得近了点"，而不是一个死循环。
        /// </remarks>
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
