using DeepseaOil.Logic.Events;
using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// 战斗 HUD：血量 / 水球数 / 波次与存活数；纯事件驱动，不轮询、没有 <c>Update</c>。
    /// </summary>
    /// <remarks>
    /// 面板异步加载（<c>UIMgr</c> 走协程，至少晚一帧）且事实事件只在值变化时发布：<c>ShowMe</c> 必须主动发 <c>RequestHudRefresh</c>，否则出现时是上一局的值或一片空白；订阅在前、请求在后。
    /// 不订阅暂停事件：HUD 显示的是状态而不是"正在发生什么"，暂停时保持最后一帧的读数正是想要的行为。
    /// 子物体命名即接线（<c>BasePanel</c> 按名字建索引）：三个 TMP 文本必须命名为 <c>txtHp</c> / <c>txtWater</c> / <c>txtWave</c>，否则取不到（会打警告）。
    /// </remarks>
    public sealed class HudPanel : BasePanel
    {
        private const string HpTextName = "txtHp";

        private const string WaterTextName = "txtWater";

        private const string WaveTextName = "txtWave";

        private TMP_Text _hp;
        private TMP_Text _water;
        private TMP_Text _wave;

        private bool _subscribed;

        /// <summary>
        /// 放 Bottom 是为了不挡 Esc：<c>UIMgr.TryCloseTopmostPanel</c> 按 <c>System → Top → Middle → Bottom</c> 找第一个活着的面板，遇 <c>CanBeHideByKey == false</c> 就此停下。
        /// </summary>
        public override E_UILayer Layer => E_UILayer.Bottom;

        public override bool CanBeHideByKey => false;

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
