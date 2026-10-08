using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Projectile;
using DeepseaOil.Presentation.Visual;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Ball
{
    /// <remarks>造 → Tick → Dispose 各只有一个调用方 BallDirector，销毁 GameObject 也归它。观感与冲量参数取自注入的 ProjectileSpec.Tuning，不读全局 SO。</remarks>
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

            // 根物体悬空：贴地位置再抬出手高度，阴影自写世界坐标。
            _root.transform.position = new Vector3(data.Start.x, data.Start.y + originHeight, 0f);

            var view = _root.AddComponent<BallView>();

            view.Initialize(definition.BallRadius * 2f, definition.BallColor);

            // 先 view 再 shadow：AddComponent 会立刻跑子物体 Awake，顺序写死。
            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(_root.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(tuning, RenderOrder.GroundShadow, shadowRadius * 2f, definition.ShadowColor);

            _driver = _root.AddComponent<BallDriver>();
            _driver.Initialize(in data, view, shadow, originHeight, OnLandedInternal);
        }

        public void Tick(float deltaTime)
        {
            if (!IsAlive) return;

            _driver.Tick(deltaTime);
        }

        /// <summary>幂等回收本球</summary>
        public void Dispose()
        {
            IsAlive = false;

            if (_root == null) return;

            UnityEngine.Object.Destroy(_root);
        }

        /// <summary>落地：先翻存活标志，再交上层结算</summary>
        /// <remarks>顺序不能反：BallDirector 回调后立即回收本球，报存活的窗口会让回收判断多走一轮。</remarks>
        private void OnLandedInternal(Vector2 point, BallType type)
        {
            if (!IsAlive) return;

            IsAlive = false;
            _onLanded?.Invoke(this, point);
        }
    }
}
