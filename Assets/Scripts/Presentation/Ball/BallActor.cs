using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Projectile;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>
    /// 一颗投掷球的<b>表现侧实体</b>：纯 C# 组合件（不是 MonoBehaviour），
    /// 包住飞行（<see cref="BallDriver"/>）＋ 本体（<see cref="BallView"/>）＋ 投影（<see cref="BallShadow"/>）。
    /// </summary>
    /// <remarks>
    /// <b>为什么是组合件而不是再挂一层 MonoBehaviour：</b>三件套各自已经是 MonoBehaviour
    /// （渲染与组件模型要求如此），再挂一层只会让"谁在驱动谁"多一层间接。
    /// 它由 <c>BallDirector</c> 造、驱动、回收：<b>造 → Tick → Dispose</b> 各只有一个调用方。
    /// <para><b>资产自持：</b>球的观感（图元、颜色、直径、阴影色）由本类自己决定 ——
    /// 将来换正式美术（Sprite / 预制体）时改的是这里，不是每个调用点。白模阶段用程序化图元。</para>
    /// <para><b>它不销毁 GameObject：</b><see cref="BallDriver"/> 落地时只回调，由
    /// <c>BallDirector</c> 统一 <see cref="Dispose"/>。两个地方都能销毁，就会出现"谁先谁后"的竞态 ——
    /// 收口前那条链上正是两条销毁路径（球驱动器自毁 ＋ 控制器兜底销毁）同时存在。</para>
    /// <para><b>调参可被覆盖：</b><see cref="Tuning"/> 默认取全局 SO（由组合根注入）；
    /// 将来某种球要自定义手感，是在"球实体"这一层覆盖它，而不是给全局 SO 加一身字段。</para>
    /// </remarks>
    public sealed class BallActor
    {
        /// <summary>阴影色：贴地件的"存在感"来自它，不走球种色（阴影是光，不是材质）。</summary>
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.35f);

        private readonly GameObject _root;
        private readonly BallDriver _driver;
        private readonly Action<BallActor, Vector2> _onLanded;

        /// <summary>本球出生时的定义（取值边界）。</summary>
        public BallDefinition Definition { get; }

        /// <summary>本球使用的观感 / 冲量调参。</summary>
        public ThrowTuning Tuning { get; }

        /// <summary>球种。</summary>
        public BallType Type => Definition.Type;

        /// <summary>是否还在飞（落地的那一刻变 <c>false</c>）。</summary>
        public bool IsAlive { get; private set; }

        /// <summary>
        /// 建出球根物体与其三件套（本体 / 阴影 / 驱动器）。
        /// </summary>
        /// <param name="data">飞行数据（起点、落点、时长、弧高）。</param>
        /// <param name="definition">球定义。</param>
        /// <param name="tuning">观感与冲量调参；为 <c>null</c> 时全部走代码兜底值。</param>
        /// <param name="ballRoot">球的父物体；为 <c>null</c> 时建在场景根下。</param>
        /// <param name="onLanded">落地回调：<c>(本球, 落点)</c>。回调发生在球被回收之前。</param>
        public BallActor(
            in BallData data,
            in BallDefinition definition,
            ThrowTuning tuning,
            Transform ballRoot,
            Action<BallActor, Vector2> onLanded)
        {
            Definition = definition;
            Tuning = tuning;
            _onLanded = onLanded;
            IsAlive = true;

            float originHeight = tuning != null ? tuning.originHeight : 0f;
            float ballRadius = tuning != null ? tuning.ballRadiusMeters : 0.22f;
            float shadowRadius = tuning != null ? tuning.shadowRadiusMeters : 0.20f;

            _root = new GameObject($"球_{data.Type}");

            // 保持世界坐标：即使 ballRoot 带着位移，球也不会跟着偏。
            _root.transform.SetParent(ballRoot, true);

            // 根物体是**悬空**的：贴地位置再抬一个出手高度。球的视觉子物体用相对高度叠加，
            // 所以会继承这个抬高量（正是我们要的）；而阴影自己写世界坐标，因此不会被它带上去。
            _root.transform.position = new Vector3(data.Start.x, data.Start.y + originHeight, 0f);

            var view = _root.AddComponent<BallView>();
            view.Initialize(RenderOrder.Ball, ballRadius * 2f, CombatPalette.BallColor(data.Type));

            // 先 view 再 shadow：AddComponent 会立刻跑子物体的 Awake，顺序写死才不会让两帧的顺序飘。
            var shadowGo = new GameObject("阴影");
            shadowGo.transform.SetParent(_root.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(tuning, RenderOrder.BallShadow, shadowRadius * 2f, ShadowColor);

            _driver = _root.AddComponent<BallDriver>();
            _driver.Initialize(in data, view, shadow, originHeight, OnLandedInternal);
        }

        /// <summary>推进一个渲染帧（由 <c>BallDirector</c> 调）。</summary>
        /// <param name="deltaTime">本帧时长；暂停时为 0，球自然冻结。</param>
        public void Tick(float deltaTime)
        {
            if (!IsAlive) return;

            _driver.Tick(deltaTime);
        }

        /// <summary>回收本球（销毁它的 GameObject）。<b>幂等</b>：重复调用不会重复销毁。</summary>
        public void Dispose()
        {
            IsAlive = false;

            if (_root == null) return;

            UnityEngine.Object.Destroy(_root);
        }

        /// <summary>
        /// 落地：<b>先翻自己的存活标志，再交给上层结算</b>。
        /// </summary>
        /// <remarks>
        /// 顺序不能反：结算方（<c>BallDirector</c>）会在回调之后立刻回收本球，
        /// 若标志留在回调之后翻，那个"已经落地但还报存活"的窗口会让回收判断多走一轮。
        /// </remarks>
        private void OnLandedInternal(Vector2 point, BallType type)
        {
            if (!IsAlive) return;

            IsAlive = false;
            _onLanded?.Invoke(this, point);
        }
    }
}
