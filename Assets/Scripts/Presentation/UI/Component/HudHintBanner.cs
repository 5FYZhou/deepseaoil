using System.Collections;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Level;
using DeepseaOil.Logic.Wave;
using DeepseaOil.Presentation.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using cfg.dso;

namespace DeepseaOil.Presentation.UI
{
    public sealed class HudHintBanner : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private const float LowHealthFraction = 0.4f;
        private const float LowHealthDelaySeconds = 8.5f;
        private const float LowHealthCooldownSeconds = 30f;
        private const int MaxLowHealthWarnings = 3;
        private const float BannerHeight = 68f;
        private const float BannerPadding = 64f;
        private const float BannerMinWidth = 360f;
        private const float BannerMaxWidth = 1400f;
        private static readonly Color BannerBackground = new Color32(52, 52, 51, 240);
        private static readonly Color BannerText = new Color32(247, 240, 231, 255);
        private static readonly Color BannerBorder = new Color32(89, 90, 88, 255);
        private static readonly Color LowHealthAccent = new Color32(218, 104, 76, 255);
        private static readonly Color SupportAccent = new Color32(133, 198, 200, 255);
        private static readonly Color TextOutlineColor = new Color32(30, 30, 29, 255);

        [SerializeField] private float slideSeconds = 0.2f;
        [SerializeField] private float defaultHoldSeconds = 4.2f;

        private RectTransform _rect;
        private GameRoot _root;
        private CanvasGroup _group;
        private TMP_Text _text;
        private Outline _border;
        private Image _accent;
        private Coroutine _animation;
        private Vector2 _basePosition;
        private float _holdRemaining;
        private float _lowHealthSeconds;
        private float _warningCooldown;
        private float _healthCurrent;
        private float _healthMax;
        private int _warningCount;
        private int _activePriority;
        private bool _paused;
        private bool _registered;
        private bool _built;

        public int Order => SceneOrder.World + 30;

        public void Attach()
        {
        }

        public void Initialize(TMP_Text fontSource)
        {
            EnsureBuilt(fontSource);
        }

        private void Awake()
        {
            EnsureBuilt(FindFontSource());
        }

        private void OnEnable()
        {
            EventBus<WavePhaseChanged>.Subscribe(OnWavePhaseChanged);
            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
            EventBus<PlayerAmmoChanged>.Subscribe(OnAmmoChanged);
            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            EventBus<LevelStateChanged>.Subscribe(OnLevelStateChanged);
            Register();
        }

        private void OnDisable()
        {
            EventBus<WavePhaseChanged>.Unsubscribe(OnWavePhaseChanged);
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            EventBus<PlayerAmmoChanged>.Unsubscribe(OnAmmoChanged);
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChanged);
            EventBus<LevelStateChanged>.Unsubscribe(OnLevelStateChanged);
            Unregister();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_animation != null)
                StopCoroutine(_animation);
        }

        public void RenderTick(float deltaTime)
        {
            if (!IsGameplayActive())
            {
                _lowHealthSeconds = 0f;
                HideImmediate();
                return;
            }

            if (_warningCooldown > 0f)
                _warningCooldown = Mathf.Max(0f, _warningCooldown - deltaTime);

            bool lowHealth = IsLowHealth(_healthCurrent, _healthMax);
            bool inLife = IsInLifeFountain();

            if (!lowHealth || inLife || IsStoryState())
            {
                _lowHealthSeconds = 0f;
                return;
            }

            _lowHealthSeconds += deltaTime;

            if (ShouldShowLowHealth(
                    _healthCurrent,
                    _healthMax,
                    inLife,
                    true,
                    _lowHealthSeconds,
                    LowHealthDelaySeconds,
                    _warningCooldown,
                    _warningCount))
            {
                _lowHealthSeconds = 0f;
                _warningCooldown = LowHealthCooldownSeconds;
                _warningCount++;

                ShowPrompt(
                    "<b><color=#da684c>血量告急</color></b>，跟着绿色标识去<color=#7eb88a><b>【生命泉】</b></color>站稳回血",
                    priority: 30,
                    holdSeconds: 6f);
            }
        }

        public static bool IsLowHealth(float current, float max)
        {
            return current > 0f && max > 0f && current / max <= LowHealthFraction;
        }

        public static bool ShouldShowLowHealth(
            float current,
            float max,
            bool inLifeFountain,
            bool gameplayActive,
            float lowSeconds,
            float delaySeconds,
            float cooldown,
            int warningCount)
        {
            return gameplayActive
                && !inLifeFountain
                && IsLowHealth(current, max)
                && lowSeconds >= delaySeconds
                && cooldown <= 0f
                && warningCount < MaxLowHealthWarnings;
        }

        private void ShowPrompt(string message, int priority, float holdSeconds)
        {
            EnsureBuilt(FindFontSource());

            if (!IsGameplayActive())
                return;

            if (_group.alpha > 0f && priority < _activePriority)
                return;

            _activePriority = priority;
            _holdRemaining = Mathf.Max(slideSeconds, holdSeconds);
            _text.text = message;
            _text.ForceMeshUpdate();
            float width = Mathf.Clamp(_text.preferredWidth + BannerPadding, BannerMinWidth, BannerMaxWidth);
            _rect.sizeDelta = new Vector2(width, BannerHeight);
            _accent.color = priority >= 30 ? LowHealthAccent : SupportAccent;
            _text.gameObject.SetActive(true);
            _rect.gameObject.SetActive(true);

            if (_animation != null)
                StopCoroutine(_animation);

            _animation = StartCoroutine(CoPlay());

            _root?.Audio?.PlayUiSlide(true);
        }

        private IEnumerator CoPlay()
        {
            _group.alpha = 0f;
            _rect.anchoredPosition = _basePosition + Vector2.up * 18f;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, slideSeconds);

            while (elapsed < duration)
            {
                if (!_paused)
                    elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);

                _group.alpha = eased;
                _rect.anchoredPosition = Vector2.LerpUnclamped(
                    _basePosition + Vector2.up * 18f,
                    _basePosition,
                    eased);

                yield return null;
            }

            _group.alpha = 1f;
            _rect.anchoredPosition = _basePosition;

            while (_holdRemaining > 0f)
            {
                if (!_paused)
                    _holdRemaining -= Time.unscaledDeltaTime;

                yield return null;
            }

            elapsed = 0f;

            while (elapsed < duration)
            {
                if (!_paused)
                    elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(elapsed / duration);

                _group.alpha = 1f - progress;
                _rect.anchoredPosition = _basePosition + Vector2.up * (18f * progress);

                yield return null;
            }

            _animation = null;
            _activePriority = 0;
            HideImmediate();
        }

        private void EnsureBuilt(TMP_Text fontSource)
        {
            if (_built)
            {
                if (fontSource != null && _text.font != fontSource.font)
                {
                    _text.font = fontSource.font;
                    _text.fontSharedMaterial = fontSource.fontSharedMaterial;
                    _text.ForceMeshUpdate();
                }

                return;
            }

            if (transform is not RectTransform rootRect)
                return;

            var go = new GameObject(
                "HudHintBanner",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(Image));

            _rect = go.GetComponent<RectTransform>();
            _rect.SetParent(rootRect, false);
            _rect.anchorMin = new Vector2(0.5f, 1f);
            _rect.anchorMax = new Vector2(0.5f, 1f);
            _rect.pivot = new Vector2(0.5f, 1f);
            _rect.anchoredPosition = new Vector2(0f, -88f);
            _rect.sizeDelta = new Vector2(BannerMinWidth, BannerHeight);
            _basePosition = _rect.anchoredPosition;

            Image background = go.GetComponent<Image>();
            background.color = BannerBackground;
            background.raycastTarget = false;
            _border = go.AddComponent<Outline>();
            _border.effectColor = BannerBorder;
            _border.effectDistance = new Vector2(1.6f, -1.6f);
            _border.useGraphicAlpha = true;

            var accentGo = new GameObject(
                "HintAccent",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            RectTransform accentRect = accentGo.GetComponent<RectTransform>();
            accentRect.SetParent(_rect, false);
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(14f, 0f);
            accentRect.sizeDelta = new Vector2(4f, -20f);

            _accent = accentGo.GetComponent<Image>();
            _accent.color = SupportAccent;
            _accent.raycastTarget = false;

            _group = go.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var textGo = new GameObject(
                "TxtHint",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(_rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(38f, 8f);
            textRect.offsetMax = new Vector2(-28f, -8f);

            _text = textGo.GetComponent<TMP_Text>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.fontSize = 24f;
            _text.color = BannerText;
            _text.raycastTarget = false;

            Outline textOutline = textGo.AddComponent<Outline>();
            textOutline.effectColor = TextOutlineColor;
            textOutline.effectDistance = new Vector2(1.2f, -1.2f);
            textOutline.useGraphicAlpha = true;

            if (fontSource != null)
            {
                _text.font = fontSource.font;
                _text.fontSharedMaterial = fontSource.fontSharedMaterial;
            }

            go.SetActive(false);
            _built = true;
        }

        private TMP_Text FindFontSource()
        {
            Transform source = transform.Find("TxtNextWave");

            if (source == null)
                source = transform.Find("TxtCountdown");

            return source != null ? source.GetComponent<TMP_Text>() : null;
        }

        private bool IsInLifeFountain()
        {
            CombatRoot combat = CombatRoot.Current;

            if (combat == null)
                return false;

            for (int i = 0; i < combat.Fountains.Count; i++)
            {
                Fountain fountain = combat.Fountains[i];

                if (fountain != null
                    && fountain.Kind == FountainKind.Life
                    && fountain.PlayerInside)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGameplayActive()
        {
            return _root != null
                && _root.Game != null
                && (_root.Game.CurState == GameState.Running
                    || _root.Game.CurState == GameState.Story);
        }

        private bool IsStoryState()
        {
            return _root != null && _root.Game != null && _root.Game.CurState == GameState.Story;
        }

        private void OnWavePhaseChanged(WavePhaseChanged evt)
        {
            if (evt.Phase != WavePhase.Prep || evt.GrantSeed == SeedType.None)
                return;

            SeedSpec seed = ConfigModule.GetSeed(evt.GrantSeed);
            string name = seed != null ? seed.Name : evt.GrantSeed.ToString();

            ShowPrompt(
                $"支援送达，获得<color=#85c6c8><b>【{name}】</b></color>，按 E 砸地配合水土触发反应",
                priority: 10,
                holdSeconds: defaultHoldSeconds);
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            _healthCurrent = evt.Current;
            _healthMax = evt.Max;
        }

        private void OnAmmoChanged(PlayerAmmoChanged evt)
        {
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            _paused = evt.state == GameState.Paused;

            if (evt.state != GameState.Running && evt.state != GameState.Story)
                HideImmediate();
        }

        private void OnLevelStateChanged(LevelStateChanged evt)
        {
            _lowHealthSeconds = 0f;
            _warningCooldown = 0f;
            _warningCount = 0;
            HideImmediate();
        }

        private void HideImmediate()
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            _holdRemaining = 0f;
            _activePriority = 0;

            if (_group != null)
                _group.alpha = 0f;

            if (_rect != null)
            {
                _rect.anchoredPosition = _basePosition;
                _rect.gameObject.SetActive(false);
            }
        }

        private void Register()
        {
            if (_registered)
                return;

            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            _root?.UnregisterSceneRoot(this);
            _root = null;
            _registered = false;
        }
    }
}
