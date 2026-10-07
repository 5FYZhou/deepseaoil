using System;
using DeepseaOil.Data;
using DeepseaOil.Logic.Projectile;
using UnityEngine;

namespace DeepseaOil.Presentation.Projectile
{
    /// <summary>
    /// 推进一颗球的飞行进度，每帧把两个视效件摆到位，<c>t ≥ 1</c> 时回调落地。
    /// </summary>
    /// <remarks>
    /// <b>本类没有 <c>Update</c>，也不查任何碰撞体</b>：落点由 <see cref="ProjectileTrajectory"/> 自己算出来，
    /// 落地判定就是一次 <c>t ≥ 1</c>。需求已定"命中判定只按落地结算，不做飞行中检测"，
    /// 所以球不需要物理体、不需要刚体、不需要射线，飞行是纯视觉的。
    /// <para><b>由组合根每帧调 <see cref="Tick"/></b>（不是自驱 <c>Update</c>）：
    /// 框架的硬契约是"每帧只有四个驱动入口"，自驱会让帧内顺序变成不可预测；
    /// 而 <c>dt</c> 由调用方给（<c>Time.deltaTime</c>）⇒ 暂停时球自然冻结、恢复后从原进度继续。</para>
    /// <para><b>落地回调而不是 EventBus：</b>球与落点结算是同一次投掷的两半，
    /// 走全局事件总线只会让"谁接了我的球"变成要靠搜代码回答的问题。回调让接线在组合根里一眼可见。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BallDriver : MonoBehaviour
    {
        private ProjectileTrajectory _data;
        private BallView _view;
        private BallShadow _shadow;
        private Action<Vector2, cfg.demo.BallType> _onLanded;

        /// <summary>
        /// 出手抬高量（世界单位）：**只影响视觉，不参与落点与飞行时长**。
        /// </summary>
        /// <remarks>
        /// 刻意选择，不是随手加的偏移：不抬高的话，起手的球会从玩家身体里钻出来。
        /// <para><b>它不写进 <c>ProjectileTrajectory.Start</c> / <c>ProjectileTrajectory.End</c></b> —— 那两个值是"贴地的逻辑位置"，
        /// 阴影贴的就是它们。抬高量只在画球时叠加上去。曾经把它烘进 Start/End，
        /// 结果是阴影也跟着往上跑：阴影看起来"飘在球下面一点"、还跟着抛物线上下起伏。</para>
        /// </remarks>
        private float _originHeight;

        /// <summary>已飞比例，0..1。超过 1 的那一帧直接结算落地。</summary>
        private float _t;

        /// <summary>
        /// 组装。依赖全部由参数给出（不留 inspector 字段），所以"忘了接线"这种失败模式在本类不存在。
        /// </summary>
        /// <param name="data">飞行数据。</param>
        /// <param name="view">球本体视效。</param>
        /// <param name="shadow">阴影视效。</param>
        /// <param name="originHeight">出手抬高量（来自 <c>ThrowTuning.originHeight</c>）。</param>
        /// <param name="onLanded">落地回调：<c>(落点, 球种)</c>。</param>
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

        /// <summary>推进一个渲染帧。</summary>
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

        /// <summary>
        /// 把两个视效件按当前进度摆到位。两者吃的是<b>同一份贴地逻辑位置</b>，只有高度不同。
        /// </summary>
        /// <remarks>
        /// 分工是本类的核心：<b>阴影只吃弧高，球本体吃"固定的出手抬高量 + 弧高"</b>。
        /// 球本体的抬高量是让它别从玩家身体里钻出来，弧高才是抛物线。
        /// 两者混用过的后果见 <c>ThrowTuning.originHeight</c> 的注释。
        /// </remarks>
        private void ApplyAt(float t)
        {
            Vector2 ground = _data.SampleGround(t);
            float arc = _data.MaxHeight * ProjectileTrajectory.SampleHeight01(t);

            _shadow.Apply(ground, arc, _data.MaxHeight);
            _view.Apply(ground, arc + _originHeight);
        }

        /// <summary>
        /// 结算落地：回调监听方，<b>自己不做任何清理</b>。
        /// </summary>
        /// <remarks>
        /// 用 <c>t = 1</c> 采样落点，而不是用"最后一帧的进度"：回调方拿到的一定是精确的落点，
        /// 与指示器画的位置逐位一致。否则玩家会看到"指示圈在这里、冲量生效在那里"，
        /// 差一帧的落点在最远处能差 0.4 米以上。
        /// <para><b>为什么不再自己 <c>Destroy</c>：</b>回收权归持有者（<c>BallActor</c> →
        /// <c>BallDirector</c>）。收口前"驱动器自毁 ＋ 控制器兜底销毁"是两条销毁路径，
        /// 谁也说不清某一帧谁先跑；现在只有一个答案。</para>
        /// </remarks>
        private void Land()
        {
            Vector2 landing = _data.SampleGround(1f);

            _onLanded?.Invoke(landing, _data.Type);
        }
    }
}
