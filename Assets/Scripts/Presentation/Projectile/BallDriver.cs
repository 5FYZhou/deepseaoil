using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <remarks>视效件位置由轨迹数据算出，从不读回 transform，故无累积漂移。</remarks>
    [DisallowMultipleComponent]
    public sealed class BallDriver : MonoBehaviour
    {
        /// <summary>姿态折算系数与倾角上限</summary>
        private const float PitchScale = 0.65f;

        private const float PitchLimitDegrees = 35f;

        private ProjectileTrajectory _data;
        private BallView _view;
        private BallShadow _shadow;
        private Action<Vector2, cfg.dso.BallType> _onLanded;

        private float _originHeight;

        /// <summary>已飞比例；超过 1 的那一帧结算落地</summary>
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
            // 时长为 0 来自非数输入，直接结算，免得除出非数
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

            // 抛物线切线速度分量：Vx 恒定，Vy 含弧高导数
            float vx = _data.End.x - _data.Start.x;
            float arcDerivative = 4f * (1f - 2f * Mathf.Clamp01(t));
            float vy = (_data.End.y - _data.Start.y) + _data.MaxHeight * arcDerivative;

            // 侧腹迎风：切线姿态会让瓶身尖头朝前扎
            float flightAngle = Mathf.Atan2(vy, Mathf.Abs(vx)) * Mathf.Rad2Deg;
            float pitchZ = Mathf.Clamp(flightAngle * PitchScale, -PitchLimitDegrees, PitchLimitDegrees);

            // 倾角方向跟水平投掷方向走：BallView 靠它的符号翻瓶腹
            float facingSign = vx < 0f ? -1f : 1f;

            _shadow.Apply(ground, arc, _data.MaxHeight);
            _view.Apply(ground, arc + _originHeight, pitchZ * facingSign);
        }

        /// <summary>结算落地：回调监听方，自己不清理</summary>
        private void Land()
        {
            Vector2 landing = _data.SampleGround(1f);

            _onLanded?.Invoke(landing, _data.Type);
        }
    }
}
