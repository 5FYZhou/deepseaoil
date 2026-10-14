using cfg.dso;
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
        private const float MaterialAtOneHealth = 0.18f;
        private const float MaterialAtTwoHealth = 0.28f;
        private const float MaterialAtThreeHealth = 0.395f;
        private const float HealthAnimDuration = 0.25f;
        private const float HilightTime = 0.2f;

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
        private TMP_Text _txtLevel;
        private TMP_Text _txtWater;
        private TMP_Text _txtEarth;
        private Image _imgWaterBG;
        private Image _imgEarthBG;
        private Image _imgSeedBG;
        private Image _imgWater;
        private Image _imgEarth;
        private Image _imgSlecteState;
        private Coroutine _healthAnimCoroutine;
        private Coroutine _waterIconCoroutine;
        private Coroutine _earthIconCoroutine;
        private Coroutine _seedIconCoroutine;
        private Coroutine _selectStateCoroutine;
        private int _pendingSelectedStates;
        private bool _subscribed;
        private bool _hasAmmoSnapshot;
        private int _waterAmount;
        private int _earthAmount;
        private SeedType _seedType;

        protected override void Awake()
        {
            base.Awake();

            _txtCountdown = GetComponent<TMP_Text>("TxtCountdown");
            _txtNextWave = GetComponent<TMP_Text>("TxtNextWave");
            _txtLevel = GetComponent<TMP_Text>("TxtLevel");
            _txtWater = GetComponent<TMP_Text>("TxtWater");
            _txtEarth = GetComponent<TMP_Text>("TxtEarth");
            Image healthImage = GetComponent<Image>("ImgHealth");
            _imgWaterBG = GetComponent<Image>("ImgWaterBG");
            _imgEarthBG = GetComponent<Image>("ImgEarthBG");
            _imgSeedBG = GetComponent<Image>("ImgSeedBG");
            _imgWater = GetComponent<Image>("ImgWater");
            _imgEarth = GetComponent<Image>("ImgEarth");
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
            _healthMaterial.SetFloat(HealthPropertyId, MaterialAtThreeHealth);
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

            if (_healthMaterial != null)
                Destroy(_healthMaterial);

            base.OnDestroy();
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

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnPause":
                    GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    GameRoot.Instance.UI.ShowPanel<PausePanel>();
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
            EventBus<LevelStateChanged>.Subscribe(OnLevelChanged);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            _subscribed = false;
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<PlayerAmmoChanged>.Unsubscribe(OnAmmoChanged);
            EventBus<WavePhaseChanged>.Unsubscribe(OnWavePhaseChanged);
            EventBus<WaveCountdownChanged>.Unsubscribe(OnWaveCountdownChanged);
            EventBus<LevelStateChanged>.Unsubscribe(OnLevelChanged);
        }

        private void OnWavePhaseChanged(WavePhaseChanged evt)
        {
            if (_txtNextWave == null) return;

            _txtNextWave.text = $"{evt.Phase.ToString()}";//下一波：{evt.WaveIndex}";
        }

        private void OnWaveCountdownChanged(WaveCountdownChanged evt)
        {
            if (_txtCountdown == null) return;

            _txtCountdown.gameObject.SetActive(true);
            _txtCountdown.text = $"倒计时：{evt.SecondsRemaining}";
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

            float targetHealth = GetMaterialHealth(Mathf.Clamp(evt.Current, 0f, 3f));
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

            return Mathf.Lerp(MaterialAtTwoHealth, MaterialAtThreeHealth, health - 2f);
        }

        private void OnLevelChanged(LevelStateChanged evt)
        {
            _txtLevel.text = evt.Level.ToString();
        }
    }
}