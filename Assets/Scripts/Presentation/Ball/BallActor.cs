using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Projectile;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>一颗投掷球的表现侧实体：纯 C# 组合件，包住飞行（<see cref="BallDriver"/>）＋ 本体（<see cref="BallView"/>）＋ 投影（<see cref="BallShadow"/>）。</summary>
    /// <remarks>造 → Tick → Dispose 各只有一个调用方（<c>BallDirector</c>）；本类不销毁 GameObject，销毁统一归 <c>BallDirector</c>，避免两条销毁路径的竞态。
    /// 观感与冲量参数取自 <see cref="ProjectileSpec.Tuning"/>（组合根注入），不读全局 SO。</remarks>
    public sealed class BallActor : IDrivenEntity
    {
        private readonly GameObject _root;
        private readonly BallDriver _driver;
        private readonly Action<BallActor, Vector2> _onLanded;

        public ProjectileSpec Definition { get; }

        public BallType Type => Definition.Type;

        public bool IsAlive { get; private set; }

        public BallActor(
            in ProjectileTrajectory data,
            ProjectileSpec definition,
            Transform ballRoot,
            Action<BallActor, Vector2> onLanded)
        {
            Definition = definition;
            _onLanded = onLanded;
            IsAlive = true;

            ThrowTuning tuning = definition.Tuning;

            float originHeight = tuning != null ? tuning.originHeight : 0f;
            float shadowRadius = tuning != null ? tuning.shadowRadiusMeters : 0.20f;

            _root = new GameObject($"Ball_{data.Type}");

            _root.transform.SetParent(ballRoot, true);

            // 根物体悬空：贴地位置再抬一个出手高度（视觉子物体用相对高度叠加会继承；阴影自己写世界坐标，不会）。
            _root.transform.position = new Vector3(data.Start.x, data.Start.y + originHeight, 0f);

            var view = _root.AddComponent<BallView>();

            view.Initialize(definition.BallRadius * 2f, definition.BallColor);

            // 先 view 再 shadow：AddComponent 会立刻跑子物体的 Awake，顺序写死才不会让两帧的顺序飘。
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(_root.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(tuning, RenderOrder.GroundShadow, shadowRadius * 2f, definition.ShadowColor);

            _driver = _root.AddComponent<BallDriver>();
            _driver.Initialize(in data, view, shadow, originHeight, OnLandedInternal);
        }

        /// <param name="deltaTime">本帧时长；暂停时为 0，球自然冻结。</param>
        public void Tick(float deltaTime)
        {
            if (!IsAlive) return;

            _driver.Tick(deltaTime);
        }

        /// <summary>回收本球（销毁它的 GameObject）。幂等：重复调用不会重复销毁。</summary>
        public void Dispose()
        {
            IsAlive = false;

            if (_root == null) return;

            UnityEngine.Object.Destroy(_root);
        }

        /// <summary>落地：先翻自己的存活标志，再交给上层结算。</summary>
        /// <remarks>顺序不能反：<c>BallDirector</c> 在回调后立刻回收本球，"已落地但仍报存活"的窗口会让回收判断多走一轮。</remarks>
        private void OnLandedInternal(Vector2 point, BallType type)
        {
            if (!IsAlive) return;

            IsAlive = false;
            _onLanded?.Invoke(this, point);
        }
    }
}
