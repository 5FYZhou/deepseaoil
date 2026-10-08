using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <remarks>每帧摆两个视效件：同一份贴地逻辑位置，高度不同（阴影=弧高，球=出手抬高量+弧高）。t≥1 回调落地。纯视觉不查碰撞体，Tick 由组合根每帧调，dt 由调用方给，暂停即冻结。</remarks>
    [DisallowMultipleComponent]
    public sealed class BallDriver : MonoBehaviour
    {
        private ProjectileTrajectory _data;
        private BallView _view;
        private BallShadow _shadow;
        private Action<Vector2, cfg.dso.BallType> _onLanded;

        private float _originHeight;

        /// <summary>已飞比例，0..1；超过 1 的那一帧结算落地</summary>
        private float _t;

        public void Initialize(
            in ProjectileTrajectory data,
            BallView view,
            BallShadow shadow,
            float originHeight,
            Action<Vector2, cfg.dso.BallType> onLanded)
        {
            _data = data;
            _view = view;
            _shadow = shadow;
            _originHeight = originHeight;
            _onLanded = onLanded;

            // 首帧先摆位，否则球在原点闪一帧再跳到出手点
            ApplyAt(0f);
        }

        public void Tick(float deltaTime)
        {
            // 时长为 0 只来自被构造成非数的输入，直接结算，不让它除出非数坐标
            if (_data.Duration <= 0f)
            {
                Land();
                return;
            }

            _t += deltaTime / _data.Duration;

            if (_t >= 1f)
            {
                Land();
                return;
            }

            ApplyAt(_t);
        }

        private void ApplyAt(float t)
        {
            Vector2 ground = _data.SampleGround(t);
            float arc = _data.MaxHeight * ProjectileTrajectory.SampleHeight01(t);

            _shadow.Apply(ground, arc, _data.MaxHeight);
            _view.Apply(ground, arc + _originHeight);
        }

        /// <summary>结算落地：回调监听方，自己不做清理（回收权归 BallActor/BallDirector）；落点 t=1 采样，与指示器逐位一致</summary>
        private void Land()
        {
            Vector2 landing = _data.SampleGround(1f);

            _onLanded?.Invoke(landing, _data.Type);
        }
    }
}
