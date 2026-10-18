using cfg.dso;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class GamePlayPanel : BasePanel
    {
        private static readonly int HealthPropertyId = Shader.PropertyToID("_Health");
        private const float MaterialAtZeroHealth = 0f;
        private const float MaterialAtOneHealth = 0.127f;
        private const float MaterialAtTwoHealth = 0.192f;
        private const float MaterialAtThreeHealth = 0.257f;
        private const float MaterialAtFourHealth = 0.326f;
        private const float MaterialAtFiveHealth = 0.395f;
        private const float HealthAnimDuration = 0.25f;
        private const float HilightTime = 0.2f;
        private const float WaveTextFadeDuration = 0.2f;
        private const float BattleTextHoldDuration = 3f;

        [SerializeField] private Sprite spriteWater;
        [SerializeField] private Sprite spriteEarth;
        [SerializeField] private Sprite spriteEmpty;
        [SerializeField] private Sprite spriteBg;
        [SerializeField] private Sprite spriteSelected;
        [SerializeField] private Sprite spriteState0;
        [SerializeField] private Sprite spriteState1;
        [SerializeField] private Sprite spriteState2;
        [SerializeField] private Sprite spriteState3;

        public override E_UILayer Layer => E_UILayer.Bottom;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => false;

        public int WaterAmount
        {
            get => _waterAmount;
            set
            {
                _waterAmount = value;
                _txtWater.text = value.ToString();
                if (value > 0)
                {
                    _imgWater.gameObject.SetActive(true);
                    _txtWater.gameObject.SetActive(true);
                }
                else
                {
                    _imgWater.gameObject.SetActive(false);
                    _txtWater.gameObject.SetActive(false);
                }
            }
        }

        public int EarthAmount
        {
            get => _earthAmount;
            set
            {
                _earthAmount = value;
                _txtEarth.text = value.ToString();
                if (value > 0)
                {
                    _imgEarth.gameObject.SetActive(true);
                    _txtEarth.gameObject.SetActive(true);
                }
                else
                {
                    _imgEarth.gameObject.SetActive(false);
                    _txtEarth.gameObject.SetActive(false);
                }
            }
        }

        private Material _healthMaterial;
        private TMP_Text _txtCountdown;
        private TMP_Text _txtNextWave;
        private TMP_Text _txtWater;
        private TMP_Text _txtEarth;
        private TMP_Text _txtSeed;
        private Image _imgWaterBG;
        private Image _imgEarthBG;
        private Image _imgSeedBG;
        private Image _imgWater;
        private Image _imgEarth;
        private Image _imgSeed;
        private Image _imgSlecteState;
        private Coroutine _healthAnimCoroutine;
        private Coroutine _waterIconCoroutine;
        private Coroutine _earthIconCoroutine;
        private Coroutine _seedIconCoroutine;
        private Coroutine _selectStateCoroutine;
        private Coroutine _waveTextCoroutine;
        private Coroutine _countdownFadeCoroutine;
        private bool _countdownShown;
        private int _pendingSelectedStates;
        private bool _subscribed;
        private bool _hasWavePhase;
        private bool _hasAmmoSnapshot;
        private int _waterAmount;
        private int _earthAmount;
        private SeedType _seedType;
        private string _seedIconKey;

        private Logic.Wave.WavePhase _curWavePhase;

        protected override void Awake()
        {
            base.Awake();

            _txtCountdown = GetComponent<TMP_Text>("TxtCountdown");
            _txtNextWave = GetComponent<TMP_Text>("TxtNextWave");
            _txtWater = GetComponent<TMP_Text>("TxtWater");
            _txtEarth = GetComponent<TMP_Text>("TxtEarth");
            _txtSeed = GetComponent<TMP_Text>("TxtSeed");
            Image healthImage = GetComponent<Image>("ImgHealth");
            _imgWaterBG = GetComponent<Image>("ImgWaterBG");
            _imgEarthBG = GetComponent<Image>("ImgEarthBG");
            _imgSeedBG = GetComponent<Image>("ImgSeedBG");
            _imgWater = GetComponent<Image>("ImgWater");
            _imgEarth = GetComponent<Image>("ImgEarth");
            _imgSeed = GetComponent<Image>("ImgSeed");
            _imgSlecteState = GetComponent<Image>("ImgSlecteState");
            _imgWater.sprite = spriteWater;
            _imgEarth.sprite = spriteEarth;

            if (healthImage == null || healthImage.material == null)
            {
                Debug.LogError("[GamePlayPanel] ImgHealth 缺少可用材质。", this);
                return;
            }

            if (!healthImage.material.HasProperty(HealthPropertyId))
            {
                Debug.LogError("[GamePlayPanel] ImgHealth 材质缺少 _Health 参数。", this);
                return;
            }

            _healthMaterial = new Material(healthImage.material);
            healthImage.material = _healthMaterial;
            _healthMaterial.SetFloat(HealthPropertyId, MaterialAtFiveHealth);
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            if (_healthAnimCoroutine != null)
                StopCoroutine(_healthAnimCoroutine);
            if (_waterIconCoroutine != null)
                StopCoroutine(_waterIconCoroutine);
            if (_earthIconCoroutine != null)
                StopCoroutine(_earthIconCoroutine);
            if (_seedIconCoroutine != null)
                StopCoroutine(_seedIconCoroutine);
            if (_selectStateCoroutine != null)
                StopCoroutine(_selectStateCoroutine);
            StopWaveText();
            StopCountdownFade();

            ReleaseSeedIcon();

            if (_healthMaterial != null)
                Destroy(_healthMaterial);

            base.OnDestroy();
        }

        public override void ShowMe()
        {
            Subscribe();
            ClearWaveHud();
            EventBus<RequestHudRefresh>.Publish(new RequestHudRefresh());
        }

        public override void HideMe()
        {
            Unsubscribe();
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtCountdown":
                case "TxtNextWave":
                    return TxtStyle.Title;
            }
            return TxtStyle.Title;
        }

        protected override string SetInitialTxt(string name)
        {
            return "";
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnPause":
                    GameState state = GameRoot.Instance.Game.CurState;
                    if (state == GameState.Running || state == GameState.Story)
                        GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    break;
            }
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            _subscribed = true;
            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
            EventBus<PlayerAmmoChanged>.Subscribe(OnAmmoChanged);
            EventBus<WavePhaseChanged>.Subscribe(OnWavePhaseChanged);
            EventBus<WaveCountdownChanged>.Subscribe(OnWaveCountdownChanged);
            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            EventBus<LevelStateChanged>.Subscribe(OnLevelStateChanged);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            _subscribed = false;
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<PlayerAmmoChanged>.Unsubscribe(OnAmmoChanged);
            EventBus<WavePhaseChanged>.Unsubscribe(OnWavePhaseChanged);
            EventBus<WaveCountdownChanged>.Unsubscribe(OnWaveCountdownChanged);
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChanged);
            EventBus<LevelStateChanged>.Unsubscribe(OnLevelStateChanged);
        }

        private void OnWavePhaseChanged(WavePhaseChanged evt)
        {
            _hasWavePhase = true;
            _curWavePhase = evt.Phase;
            ApplyWaveText(evt.Phase);
        }

        // 换关卡不重开面板，靠它清残留
        private void OnLevelStateChanged(LevelStateChanged evt)
        {
            ClearWaveHud();
            _hasAmmoSnapshot = false;
            ResetHealthHud();
        }
        private void ResetHealthHud()
        {
            if (_healthAnimCoroutine != null)
            {
                StopCoroutine(_healthAnimCoroutine);
                _healthAnimCoroutine = null;
            }

            if (_healthMaterial != null)
                _healthMaterial.SetFloat(HealthPropertyId, MaterialAtFiveHealth);
        }

        private void ClearWaveHud()
        {
            if (_txtNextWave == null || _txtCountdown == null) return;

            StopWaveText();
            StopCountdownFade();

            _hasWavePhase = false;
            _curWavePhase = Logic.Wave.WavePhase.Prep;
            _countdownShown = false;

            _txtNextWave.text = "";
            _txtNextWave.alpha = 0f;

            _txtCountdown.text = "";
            _txtCountdown.alpha = 0f;
            _txtCountdown.gameObject.SetActive(false);
        }

        private void HideWaveHud()
        {
            if (_txtNextWave == null || _txtCountdown == null) return;

            StopWaveText();
            StopCountdownFade();

            _countdownShown = false;

            _txtNextWave.text = "";
            _txtNextWave.alpha = 0f;

            _txtCountdown.text = "";
            _txtCountdown.alpha = 0f;
            _txtCountdown.gameObject.SetActive(false);
        }

        private void ApplyWaveText(Logic.Wave.WavePhase phase)
        {
            if (_txtNextWave == null) return;

            switch (phase)
            {
                case Logic.Wave.WavePhase.Prep:
                    ShowWaveText("战斗准备", 0f);
                    break;
                case Logic.Wave.WavePhase.Battle:
                    ShowWaveText("战斗！", BattleTextHoldDuration);
                    break;
                case Logic.Wave.WavePhase.Settle:
                    ShowWaveText("结算", 0f);
                    break;
                default:
                    ShowWaveText("", 0f);
                    break;
            }
        }

        private void ShowWaveText(string text, float visibleSeconds)
        {
            StopWaveText();
            _waveTextCoroutine = StartCoroutine(PlayWaveText(text, visibleSeconds));
        }

        private void StopWaveText()
        {
            if (_waveTextCoroutine == null) return;

            StopCoroutine(_waveTextCoroutine);
            _waveTextCoroutine = null;
        }

        private IEnumerator PlayWaveText(string text, float visibleSeconds)
        {
            if (!string.IsNullOrEmpty(_txtNextWave.text))
                yield return FadeTextAlpha(_txtNextWave, 0f);
            else
                _txtNextWave.alpha = 0f;

            _txtNextWave.text = text;

            if (!string.IsNullOrEmpty(text))
            {
                yield return FadeTextAlpha(_txtNextWave, 1f);

                if (visibleSeconds > 0f)
                {
                    float hold = visibleSeconds - WaveTextFadeDuration;
                    if (hold > 0f)
                        yield return new WaitForSecondsRealtime(hold);

                    yield return FadeTextAlpha(_txtNextWave, 0f);
                }
            }

            _waveTextCoroutine = null;
        }

        private IEnumerator FadeTextAlpha(TMP_Text text, float target)
        {
            float start = text.alpha;
            if (Mathf.Approximately(start, target))
            {
                text.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < WaveTextFadeDuration)
            {
                // 不吃 timeScale
                elapsed += Time.unscaledDeltaTime;
                text.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / WaveTextFadeDuration));
                yield return null;
            }

            text.alpha = target;
        }

        private void OnWaveCountdownChanged(WaveCountdownChanged evt)
        {
            if (_txtCountdown == null) return;

            bool shouldShow = _curWavePhase != Logic.Wave.WavePhase.Battle;
            if (shouldShow)
            {
                _txtCountdown.text = $"倒计时：{evt.SecondsRemaining}";
                if (evt.SecondsRemaining == 3 && _curWavePhase == Logic.Wave.WavePhase.Prep)
                {
                    GameRoot.Instance.Audio.EnqueueSfx(AudioId.WaveCountdown);
                }
            }

            if (shouldShow == _countdownShown) return;

            _countdownShown = shouldShow;
            PlayCountdownFade(shouldShow);
        }

        private void PlayCountdownFade(bool show)
        {
            StopCountdownFade();

            if (show)
            {
                _txtCountdown.gameObject.SetActive(true);
                _txtCountdown.alpha = 0f;
            }
            else if (!_txtCountdown.gameObject.activeSelf)
            {
                return;
            }

            _countdownFadeCoroutine = StartCoroutine(FadeCountdown(show));
        }

        private void StopCountdownFade()
        {
            if (_countdownFadeCoroutine == null) return;

            StopCoroutine(_countdownFadeCoroutine);
            _countdownFadeCoroutine = null;
        }

        private IEnumerator FadeCountdown(bool show)
        {
            yield return FadeTextAlpha(_txtCountdown, show ? 1f : 0f);

            if (!show)
                _txtCountdown.gameObject.SetActive(false);

            _countdownFadeCoroutine = null;
        }

        private void OnAmmoChanged(PlayerAmmoChanged evt)
        {
            bool hasSnapshot = _hasAmmoSnapshot;
            bool initialize = !hasSnapshot;
            bool waterChanged = hasSnapshot && evt.Water != WaterAmount;
            bool earthChanged = hasSnapshot && evt.Earth != EarthAmount;
            bool seedChanged = hasSnapshot && evt.Seed != _seedType;

            WaterAmount = evt.Water;
            EarthAmount = evt.Earth;
            _seedType = evt.Seed;
            _hasAmmoSnapshot = true;

            if (initialize || seedChanged)
                UpdateSeedIcon(evt.Seed);

            if (_txtSeed != null)
            {
                _txtSeed.text = evt.SeedCount.ToString();
                _txtSeed.gameObject.SetActive(evt.SeedCount > 0);
            }

            if (_imgWaterBG != null)
                UpdateAmmoImage(_imgWaterBG, evt.Water > 0, waterChanged, initialize, ref _waterIconCoroutine);
            if (_imgEarthBG != null)
                UpdateAmmoImage(_imgEarthBG, evt.Earth > 0, earthChanged, initialize, ref _earthIconCoroutine);
            if (_imgSeedBG != null)
                UpdateAmmoImage(_imgSeedBG, evt.Seed != SeedType.None, seedChanged, initialize, ref _seedIconCoroutine);

            if (waterChanged)
                QueueSelectedState(1);
            if (earthChanged)
                QueueSelectedState(2);
            if (seedChanged)
                QueueSelectedState(4);
        }

        private void UpdateSeedIcon(SeedType seed)
        {
            if (_imgSeed == null) return;

            Data.SeedSpec spec = seed == SeedType.None ? null : Data.ConfigModule.GetSeed(seed);
            if (spec == null || string.IsNullOrEmpty(spec.IconKey))
            {
                ReleaseSeedIcon();
                _imgSeed.sprite = null;
                _imgSeed.enabled = false;
                return;
            }

            if (spec.IconKey == _seedIconKey)
            {
                _imgSeed.enabled = _imgSeed.sprite != null;
                return;
            }

            ReleaseSeedIcon();
            _imgSeed.sprite = Data.AssetModule.Load<Sprite>(spec.IconKey);
            _imgSeed.enabled = _imgSeed.sprite != null;

            if (_imgSeed.enabled && Data.AssetModule.TryGet<Sprite>(spec.IconKey, out _))
                _seedIconKey = spec.IconKey;
        }

        private void ReleaseSeedIcon()
        {
            if (string.IsNullOrEmpty(_seedIconKey)) return;

            Data.AssetModule.Release(_seedIconKey);
            _seedIconKey = null;
        }

        private void QueueSelectedState(int stateFlag)
        {
            if (_imgSlecteState == null) return;

            _pendingSelectedStates |= stateFlag;
            if (_selectStateCoroutine == null)
                _selectStateCoroutine = StartCoroutine(ResetSelectedState());
        }

        private IEnumerator ResetSelectedState()
        {
            while (_pendingSelectedStates != 0)
            {
                Sprite stateSprite;
                if ((_pendingSelectedStates & 1) != 0)
                {
                    _pendingSelectedStates &= ~1;
                    stateSprite = spriteState1;
                }
                else if ((_pendingSelectedStates & 2) != 0)
                {
                    _pendingSelectedStates &= ~2;
                    stateSprite = spriteState2;
                }
                else
                {
                    _pendingSelectedStates &= ~4;
                    stateSprite = spriteState3;
                }

                _imgSlecteState.sprite = stateSprite;
                yield return new WaitForSeconds(HilightTime);
            }

            _imgSlecteState.sprite = spriteState0;
            _selectStateCoroutine = null;
        }

        private void UpdateAmmoImage(Image image, bool hasValue, bool valueChanged, bool initialize, ref Coroutine iconCoroutine)
        {
            if (initialize)
            {
                image.sprite = hasValue ? spriteBg : spriteEmpty;
                return;
            }

            if (!valueChanged) return;

            if (iconCoroutine != null)
                StopCoroutine(iconCoroutine);

            image.sprite = spriteSelected;
            iconCoroutine = StartCoroutine(ResetAmmoImage(image, hasValue));
        }

        private IEnumerator ResetAmmoImage(Image image, bool hasValue)
        {
            yield return new WaitForSeconds(HilightTime);
            image.sprite = hasValue ? spriteBg : spriteEmpty;

            if (image == _imgWaterBG)
                _waterIconCoroutine = null;
            else if (image == _imgEarthBG)
                _earthIconCoroutine = null;
            else if (image == _imgSeedBG)
                _seedIconCoroutine = null;
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            if (_healthMaterial == null) return;

            float targetHealth = GetMaterialHealth(Mathf.Clamp(evt.Current, 0f, 5f));
            if (_healthAnimCoroutine != null)
            {
                StopCoroutine(_healthAnimCoroutine);
                _healthAnimCoroutine = null;
            }

            float currentHealth = _healthMaterial.GetFloat(HealthPropertyId);
            if (Mathf.Approximately(currentHealth, targetHealth))
            {
                _healthMaterial.SetFloat(HealthPropertyId, targetHealth);
                return;
            }

            _healthAnimCoroutine = StartCoroutine(
                AnimateHealth(currentHealth, targetHealth)
            );
        }

        private IEnumerator AnimateHealth(float startHealth, float targetHealth)
        {
            float elapsed = 0f;

            while (elapsed < HealthAnimDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / HealthAnimDuration);
                t = Mathf.SmoothStep(0f, 1f, t);
                _healthMaterial.SetFloat(HealthPropertyId, Mathf.Lerp(startHealth, targetHealth, t));

                yield return null;
            }

            _healthMaterial.SetFloat(HealthPropertyId, targetHealth);
            _healthAnimCoroutine = null;
        }

        private static float GetMaterialHealth(float health)
        {
            if (health <= 1f)
                return Mathf.Lerp(MaterialAtZeroHealth, MaterialAtOneHealth, health);

            if (health <= 2f)
                return Mathf.Lerp(MaterialAtOneHealth, MaterialAtTwoHealth, health - 1f);

            if (health <= 3f)
                return Mathf.Lerp(MaterialAtTwoHealth, MaterialAtThreeHealth, health - 2f);

            if (health <= 4f)
                return Mathf.Lerp(MaterialAtThreeHealth, MaterialAtFourHealth, health - 3f);

            return Mathf.Lerp(MaterialAtFourHealth, MaterialAtFiveHealth, health - 4f);
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            HideWaveHud();

            if (evt.state == GameState.Running && _hasWavePhase)
                ApplyWaveText(_curWavePhase);
        }
    }
}