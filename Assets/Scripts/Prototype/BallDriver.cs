using System;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 推进一颗球的飞行进度，每帧把两个视效件摆到位，<c>t ≥ 1</c> 时结算落地。
    /// </summary>
    /// <remarks>
    /// <b>本类没有 <c>FixedUpdate</c>，也不查任何碰撞体</b>：落点由 <see cref="BallData"/> 自己算出来，
    /// 落地判定就是一次 <c>t ≥ 1</c>。这不是偷懒 —— 需求书第六节已定"命中判定只按落地结算，不做飞行中检测"，
    /// 所以球不需要物理体、不需要刚体、不需要射线，飞行是纯视觉的。
    /// <para><b>用 <c>Time.deltaTime</c>（受 timeScale 影响）是刻意的：</b>暂停时它变 0，在飞的球自然冻结，
    /// 恢复后从原进度继续 —— 不需要订阅 <c>GamePaused</c>，也不会出现"暂停时球还在飞"。
    /// 而<b>投掷输入</b>是另一回事，那个必须显式挡掉（见 <see cref="ThrowSpawner"/>）。</para>
    /// <para><b>落地回调而不是 EventBus：</b>白模的球与"将来的效果模块"是同一段代码里的两半，
    /// 走全局事件总线只会让"谁接了我的球"变得要靠搜代码回答。回调让接线在组合根里一眼可见。
    /// 等效果模块真的独立成体系了再改总线不迟。</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BallDriver : MonoBehaviour
    {
        private BallData _data;
        private BallView _view;
        private BallShadow _shadow;
        private Action<Vector2, BallType, int> _onLanded;

        /// <summary>已飞比例，0..1。超过 1 的那一帧直接结算落地。</summary>
        private float _t;

        /// <summary>这颗球的编号，由组合根给出；原样回传给落地回调。</summary>
        private int _ballId;

        /// <summary>
        /// 组装。依赖全部由参数给出（不留 inspector 字段），所以"忘了接线"这种失败模式在本类不存在。
        /// </summary>
        /// <param name="ballId">这颗球的编号。<b>落地回调要原样带上它</b> —— 见下面的说明。</param>
        /// <param name="data">飞行数据。</param>
        /// <param name="view">球本体视效。</param>
        /// <param name="shadow">阴影视效。</param>
        /// <param name="onLanded">落地回调：<c>(落点, 球种, 球编号)</c>。</param>
        /// <remarks>
        /// <b>为什么回调要带球编号：</b>结算方要靠它回答"这颗球是不是已经打中过这个目标"。
        /// 没有它就只能用"本帧"当判据，而那要求每帧有人去复位标记 ——
        /// 于是正确性被绑死在脚本执行顺序上（见 <see cref="Damage.BallId"/>）。
        /// <para>编号由组合根维护，本类只负责原样传下去：它不该知道编号是怎么来的。</para>
        /// </remarks>
        public void Initialize(int ballId, BallData data, BallView view, BallShadow shadow, Action<Vector2, BallType, int> onLanded)
        {
            _ballId = ballId;
            _data = data;
            _view = view;
            _shadow = shadow;
            _onLanded = onLanded;

            // 首帧就把两个视效件摆到位：否则球会先在原点闪一帧，再跳到出手点。
            ApplyAt(0f);
        }

        private void Update()
        {
            // 时长为 0 只会来自"距离为 0"这种被构造成 NaN 的输入；直接结算，不让它除出非数坐标。
            if (_data.Duration <= 0f)
            {
                Land();
                return;
            }

            _t += Time.deltaTime / _data.Duration;

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
        /// 两者混用过的后果见 <see cref="ThrowConstants.THROW_ORIGIN_HEIGHT"/> 的注释。
        /// </remarks>
        private void ApplyAt(float t)
        {
            Vector2 ground = _data.SampleGround(t);
            float arc = _data.MaxHeight * BallData.SampleHeight01(t);

            _shadow.Apply(ground, arc);
            _view.Apply(ground, arc + ThrowConstants.THROW_ORIGIN_HEIGHT);
        }

        /// <summary>
        /// 结算落地：先回调（让监听方在球还"活着"的时候拿到落点），再销毁自己。
        /// </summary>
        /// <remarks>
        /// 用 <c>t = 1</c> 采样落点，而不是用"最后一帧的进度"：回调方拿到的一定是精确的
        /// <see cref="BallData.End"/>，与指示器画的位置逐位一致。否则玩家会看到"指示圈在这里、
        /// 冲量生效在那里"，差一帧的落点在最远处能差 0.4 米以上。
        /// </remarks>
        private void Land()
        {
            Vector2 landing = _data.SampleGround(1f);

            _onLanded?.Invoke(landing, _data.Type, _ballId);

            Destroy(gameObject);
        }
    }
}
