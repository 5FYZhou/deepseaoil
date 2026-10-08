using DeepseaOil.Logic.Events;
using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>战斗 HUD：血量/水球数/波次与存活数</summary>
    /// <remarks>面板异步加载且事件只在值变化时发布：ShowMe 必须发 RequestHudRefresh，否则显示上一局的值。三个 TMP 文本必须叫 txtHp / txtWater / txtWave，否则取不到。</remarks>
    public sealed class HudPanel : BasePanel
    {
        private const string HpTextName = "txtHp";

        private const string WaterTextName = "txtWater";

        private const string WaveTextName = "txtWave";

        private TMP_Text _hp;
        private TMP_Text _water;
        private TMP_Text _wave;

        private bool _subscribed;

        /// <summary>放 Bottom 不挡 Esc：关闭时按 System→Top→Middle→Bottom 找第一个能关的</summary>
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
            // 静态事件总线不会因物体销毁自动解除引用，必须显式退订。
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
