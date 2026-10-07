using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <remarks>推进一颗球的飞行进度，每帧把两个视效件摆到位（两者吃<b>同一份贴地逻辑位置</b>，只有高度不同：阴影只吃弧高，球本体吃"出手抬高量 + 弧高"），<c>t ≥ 1</c> 时回调落地；没有 <c>Update</c>、不查碰撞体（飞行纯视觉）；只能由组合根每帧调 <see cref="Tick"/>，<c>dt</c> 由调用方给 ⇒ 暂停时冻结、恢复后从原进度继续。</remarks>
    [DisallowMultipleComponent]
    public sealed class BallDriver : MonoBehaviour
    {
        private ProjectileTrajectory _data;
        private BallView _view;
        private BallShadow _shadow;
        private Action<Vector2, cfg.demo.BallType> _onLanded;

        /// <summary>出手抬高量（世界单位）：只影响视觉，不参与落点与飞行时长，也<b>不</b>写进 <c>Start</c> / <c>End</c>（贴地逻辑位置，阴影贴的就是它们）。</summary>
        private float _originHeight;

        /// <summary>已飞比例，0..1。超过 1 的那一帧直接结算落地。</summary>
        private float _t;

        public void Initialize(
            in ProjectileTrajectory data,
            BallView view,
            BallShadow shadow,
            float originHeight,
            Action<Vector2, cfg.demo.BallType> onLanded)
        {
            _data = data;
            _view = view;
            _shadow = shadow;
            _originHeight = originHeight;
            _onLanded = onLanded;

            // 首帧就把两个视效件摆到位：否则球会先在原点闪一帧，再跳到出手点。
            ApplyAt(0f);
        }

        /// <param name="deltaTime">本帧时长（<c>Time.deltaTime</c>）；暂停时为 0。</param>
        public void Tick(float deltaTime)
        {
            // 时长为 0 只会来自"距离为 0"这种被构造成非数的输入；直接结算，不让它除出非数坐标。
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

        /// <summary>结算落地：回调监听方，<b>自己不做任何清理</b>（回收权归持有者 <c>BallActor</c> → <c>BallDirector</c>）；落点用 <c>t = 1</c> 采样，与指示器画的位置逐位一致（用最后一帧的进度在最远处能差 0.4 米以上）。</summary>
        private void Land()
        {
            Vector2 landing = _data.SampleGround(1f);

            _onLanded?.Invoke(landing, _data.Type);
        }
    }
}
