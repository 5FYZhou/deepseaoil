using DeepseaOil.Logic.Events;
using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// 战斗 HUD：血量 / 水球数 / 波次与存活数。<b>纯事件驱动，没有 <c>Update</c></b>。
    /// </summary>
    /// <remarks>
    /// <b>面板不轮询</b>：它订阅事实事件（<c>XxxChanged</c>），值变了才刷。
    /// 轮询会把"每帧读一次别人的状态"变成面板的职责，而面板连那些系统在哪都不知道。
    /// <para><b>为什么在 <c>ShowMe</c> 里发一条 <c>RequestHudRefresh</c>：</b>面板是异步加载的
    /// （<c>UIMgr</c> 走协程，至少晚一帧），而事实事件只在值变化时发布 ——
    /// 不主动要一次，"面板出现时看到的是上一局的值"或者"一片空白"。
    /// 订阅在前、请求在后，所以重播一定能被自己收到。</para>
    /// <para><b>不订阅暂停事件</b>：HUD 显示的是状态而不是"正在发生什么"，
    /// 暂停时保持最后一帧的读数正是想要的行为。</para>
    /// <para><b>子物体命名即接线</b>（<c>BasePanel</c> 按名字建索引）：
    /// 三个 TMP 文本必须命名为 <c>txtHp</c> / <c>txtWater</c> / <c>txtWave</c>，否则取不到（会打警告）。</para>
    /// </remarks>
    public sealed class HudPanel : BasePanel
    {
        /// <summary>血量文本的子物体名。</summary>
        private const string HpTextName = "txtHp";

        /// <summary>水球数文本的子物体名。</summary>
        private const string WaterTextName = "txtWater";

        /// <summary>波次文本的子物体名。</summary>
        private const string WaveTextName = "txtWave";

        private TMP_Text _hp;
        private TMP_Text _water;
        private TMP_Text _wave;

        private bool _subscribed;

        /// <summary>
        /// 放在最底层。
        /// </summary>
        /// <remarks>
        /// <b>不是为了好看，是为了不挡 Esc。</b><c>UIMgr.TryCloseTopmostPanel</c> 按
        /// <c>System → Top → Middle → Bottom</c> 找第一个活着的面板，且遇到
        /// <c>CanBeHideByKey == false</c> 的面板会<b>就此停下</b>。HUD 放在 Bottom 且不可关闭，
        /// 于是它永远排在暂停面板之后被检查 —— 暂停键的行为不受它影响。
        /// </remarks>
        public override E_UILayer Layer => E_UILayer.Bottom;

        /// <summary>HUD 不该被 Esc 关掉。</summary>
        public override bool CanBeHideByKey => false;

        /// <summary>装配时把三个文本的初值写成占位符，而不是让 <c>BasePanel</c> 默认的 "Empty" 露出来。</summary>
        protected override string SetInitialTxt(string name)
        {
            switch (name)
            {
                case HpTextName:
                case WaterTextName:
                case WaveTextName:
                    return "--";

                default:
                    return base.SetInitialTxt(name);
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _hp = GetComponent<TMP_Text>(HpTextName);
            _water = GetComponent<TMP_Text>(WaterTextName);
            _wave = GetComponent<TMP_Text>(WaveTextName);
        }

        public override void ShowMe()
        {
            Subscribe();

            // 订阅在前、请求在后：重播的那三条事件必须能被自己收到。
            EventBus<RequestHudRefresh>.Publish(new RequestHudRefresh());
        }

        public override void HideMe()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            // 静态事件总线不会因物体销毁而自动解除引用，必须显式退订。
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            _subscribed = true;

            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
            EventBus<WaterBallCountChanged>.Subscribe(OnWaterChanged);
            EventBus<WaveChanged>.Subscribe(OnWaveChanged);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            _subscribed = false;

            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<WaterBallCountChanged>.Unsubscribe(OnWaterChanged);
            EventBus<WaveChanged>.Unsubscribe(OnWaveChanged);
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            if (_hp == null) return;

            _hp.text = $"血量 {Mathf.CeilToInt(evt.Current)}/{Mathf.CeilToInt(evt.Max)}";
        }

        private void OnWaterChanged(WaterBallCountChanged evt)
        {
            if (_water == null) return;

            _water.text = $"水球 {evt.Count}";
        }

        private void OnWaveChanged(WaveChanged evt)
        {
            if (_wave == null) return;

            _wave.text = $"第 {evt.WaveIndex} 波 · 敌人 {evt.Alive}";
        }
    }
}
