using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Combat;
using DeepseaOil.Presentation.Projectile;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Player
{
    /// <summary>
    /// 投掷的<b>执行器</b>：按裁决通过的意图生成一颗球，并每渲染帧推进在飞的球。
    /// </summary>
    /// <remarks>
    /// <b>它曾经装了四类职责</b>（读鼠标瞄准、弹药与冷却资格、建球与飞行、落地入队与瞄准件显示）。
    /// 收口后只剩下"生成并驱动球"这一件：瞄准与资格归玩家逻辑（战斗层），
    /// 裁决归世界侧组合根（<c>CombatRoot.RequestThrow</c>），落地结算归 <c>LandingResolver</c>。
    /// <para><b>它算出来的落点必须就是玩家瞄的那一格</b>：意图里的 <c>Cell</c> 与 <c>Target</c>
    /// 是同一次吸附计算的产物，本类<b>不重新算</b> —— 重算一次就会漂，表现是"看着能扔到、其实扔不到"。</para>
    /// <para><b>不是 <c>MonoBehaviour.Update</c> 自驱</b>：由组合根每渲染帧调 <see cref="Tick"/>，
    /// 帧内顺序因此可预测（暂停时 <c>dt = 0</c>，在飞的球自然冻住）。</para>
    /// </remarks>
    public sealed class ThrowController : MonoBehaviour
    {
        /// <summary>飞行中的球（落地即销毁，这里只做清理与每帧推进）。</summary>
        private readonly List<BallDriver> _flying = new List<BallDriver>();

        /// <summary>球种 → 配置行。</summary>
        private readonly Dictionary<BallType, BallSpec> _balls = new Dictionary<BallType, BallSpec>();

        private ThrowTuning _tuning;
        private LandingResolver _resolver;
        private Transform _ballRoot;

        /// <summary>在飞的球数（诊断 / 测试读数）。</summary>
        public int FlyingCount => _flying.Count;

        /// <summary>
        /// 装配。依赖全部由参数给出（<b>没有 inspector 字段</b>）：组合根建出本组件，
        /// 于是"忘了接线"这种失败模式在本类不存在。
        /// </summary>
        /// <param name="tuning">观感调参（球半径 / 出手高度 / 阴影）。</param>
        /// <param name="resolver">落地结算器（球落地只入队）。</param>
        /// <param name="balls">全部球种配置。</param>
        /// <param name="ballRoot">球的父物体；留空则建在场景根下。</param>
        public void Initialize(
            ThrowTuning tuning,
            LandingResolver resolver,
            IReadOnlyList<BallSpec> balls,
            Transform ballRoot)
        {
            _tuning = tuning;
            _resolver = resolver;
            _ballRoot = ballRoot;

            _balls.Clear();

            if (balls != null)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    _balls[balls[i].Type] = balls[i];
                }
            }
        }

        /// <summary>推进一个渲染帧：只推进在飞的球（瞄准与开火都不在这里）。</summary>
        /// <param name="deltaTime">本帧时长（<c>Time.deltaTime</c>）；暂停时为 0。</param>
        public void Tick(float deltaTime)
        {
            TickFlyingBalls(deltaTime);
        }

        /// <summary>
        /// 按已经裁决通过的意图真的投一颗球。
        /// </summary>
        /// <param name="intent">意图（球种 ＋ 目标格 ＋ 出手点与落点）。</param>
        /// <returns>球种没有配置行（表里少一行）时为 <c>false</c>。</returns>
        /// <remarks>调用方是 <c>CombatRoot.RequestThrow</c> —— 也就是说走到这里时
        /// "落点合法 / 玩家有资格"都已经问过了，本类不再重复判断。</remarks>
        public bool Throw(in ThrowIntent intent)
        {
            if (!_balls.TryGetValue(intent.Ball, out BallSpec spec)) return false;

            // 距离与落点取自**同一份**意图：两处各算一次必然会漂移
            float distance = Vector2.Distance(intent.Origin, intent.Target);

            var data = new BallData(intent.Ball, intent.Origin, intent.Target, distance, in spec.Throw);

            CreateBall(in data, in spec);

            return true;
        }

        /// <summary>清掉在飞的球（打空重来 / 清场）。</summary>
        public void ClearBalls()
        {
            for (int i = 0; i < _flying.Count; i++)
            {
                if (_flying[i] != null) Destroy(_flying[i].gameObject);
            }

            _flying.Clear();
        }

        /// <summary>建球根物体，并按固定顺序建球本体与阴影。</summary>
        private void CreateBall(in BallData data, in BallSpec spec)
        {
            Color color = CombatPalette.BallColor(data.Type);

            var go = new GameObject($"球_{data.Type}");

            // 保持世界坐标：即使 ballRoot 带着位移，球也不会跟着偏。
            go.transform.SetParent(_ballRoot, true);

            // 根物体是**悬空**的：贴地位置再抬一个出手高度。
            // 球的视觉子物体用相对高度叠加，所以会继承这个抬高量（正是我们要的）；
            // 而阴影自己写世界坐标，因此不会被它带上去。
            float originHeight = _tuning != null ? _tuning.originHeight : 0f;

            go.transform.position = new Vector3(data.Start.x, data.Start.y + originHeight, 0f);

            float ballRadius = _tuning != null ? _tuning.ballRadiusMeters : 0.22f;

            var view = go.AddComponent<BallView>();
            view.Initialize(RenderOrder.Ball, ballRadius * 2f, color);

            // 先 view 再 shadow：AddComponent 会立刻跑子物体的 Awake，顺序写死才不会让两帧的顺序飘。
            var shadowGo = new GameObject("阴影");
            shadowGo.transform.SetParent(go.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(
                _tuning,
                RenderOrder.BallShadow,
                (_tuning != null ? _tuning.shadowRadiusMeters : 0.20f) * 2f,
                new Color(0f, 0f, 0f, 0.35f));

            var driver = go.AddComponent<BallDriver>();
            driver.Initialize(in data, view, shadow, originHeight, OnBallLanded);

            _flying.Add(driver);
        }

        /// <summary>球落地：只入队，不在这里碰物理（冲量必须在物理帧施加）。</summary>
        private void OnBallLanded(Vector2 point, BallType type)
        {
            if (_resolver == null) return;

            if (!_balls.TryGetValue(type, out BallSpec spec)) return;

            _resolver.Enqueue(point, type, in spec);
        }

        /// <summary>推进在飞的球，并清掉已经销毁的引用。</summary>
        private void TickFlyingBalls(float deltaTime)
        {
            // 倒序：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                BallDriver ball = _flying[i];

                if (ball == null)
                {
                    _flying.RemoveAt(i);
                    continue;
                }

                ball.Tick(deltaTime);
            }
        }
    }
}
