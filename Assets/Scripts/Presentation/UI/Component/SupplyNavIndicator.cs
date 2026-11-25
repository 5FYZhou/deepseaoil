using System.Collections.Generic;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Presentation.World;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public sealed class SupplyNavIndicator : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private const int PrewarmCount = 6;
        private const float FadeSpeed = 7f;

        private sealed class NavItem
        {
            public GameObject root;
            public RectTransform rect;
            public CanvasGroup group;
            public Image image;
            public bool visible;
        }

        [SerializeField] private float iconSize = 52.2f;
        [SerializeField] private Rect safeViewport = new Rect(0.06f, 0.08f, 0.88f, 0.84f);

        private readonly List<NavItem> _items = new List<NavItem>(PrewarmCount);
        private readonly Sprite[] _kindSprites = new Sprite[3];

        private RectTransform _rootRect;
        private Canvas _canvas;
        private Camera _camera;
        private GameRoot _root;
        private Texture2D _fallbackTexture;
        private Sprite _fallbackSprite;
        private float _healthFraction = 1f;
        private bool _registered;
        private bool _built;

        public int Order => SceneOrder.World + 20;

        public void Attach()
        {
            EnsureBuilt();
        }

        public void Initialize(Sprite waterIcon, Sprite earthIcon, Sprite lifeIcon)
        {
            _kindSprites[(int)FountainKind.Water] = waterIcon;
            _kindSprites[(int)FountainKind.Earth] = earthIcon;
            _kindSprites[(int)FountainKind.Life] = lifeIcon;
        }

        private void Awake()
        {
            _rootRect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _fallbackSprite = CreateDropSprite(out _fallbackTexture);
            EnsureBuilt();
        }

        private void OnEnable()
        {
            EventBus<PlayerHealthChanged>.Subscribe(OnHealthChanged);
            Register();
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChanged>.Unsubscribe(OnHealthChanged);
            Unregister();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _kindSprites.Length; i++)
            {
                _kindSprites[i] = null;
            }

            if (_fallbackSprite != null)
                Destroy(_fallbackSprite);

            if (_fallbackTexture != null)
                Destroy(_fallbackTexture);
        }

        public void RenderTick(float deltaTime)
        {
            GameRoot gameRoot = GameRoot.Instance;

            if (gameRoot == null
                || gameRoot.Game == null
                || (gameRoot.Game.CurState != GameState.Running
                    && gameRoot.Game.CurState != GameState.Story))
            {
                HideAll(1f);
                return;
            }

            CombatRoot combat = CombatRoot.Current;
            Camera camera = ResolveCamera();

            if (combat == null || camera == null)
            {
                HideAll(deltaTime);
                return;
            }

            IReadOnlyList<Fountain> fountains = combat.Fountains;
            int used = 0;

            for (int i = 0; i < fountains.Count; i++)
            {
                Fountain fountain = fountains[i];

                if (fountain == null)
                    continue;

                NavItem item = ItemAt(used++);
                UpdateItem(item, fountain, camera, deltaTime);
            }

            for (int i = used; i < _items.Count; i++)
                FadeItem(_items[i], false, deltaTime);
        }

        public static bool TryResolveEdge(
            Vector3 viewport,
            Rect safe,
            out Vector2 edge,
            out float angle)
        {
            edge = Vector2.zero;
            angle = 0f;

            if (viewport.z <= 0f)
                return false;

            Vector2 point = viewport;

            if (safe.Contains(point))
                return false;

            Vector2 center = safe.center;
            Vector2 delta = point - center;

            float horizontal = Mathf.Abs(delta.x) > 0.00001f
                ? safe.width * 0.5f / Mathf.Abs(delta.x)
                : float.PositiveInfinity;

            float vertical = Mathf.Abs(delta.y) > 0.00001f
                ? safe.height * 0.5f / Mathf.Abs(delta.y)
                : float.PositiveInfinity;

            float scale = Mathf.Min(horizontal, vertical);

            if (float.IsInfinity(scale))
                return false;

            edge = center + delta * scale;
            angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;

            return true;
        }

        private void UpdateItem(NavItem item, Fountain fountain, Camera camera, float deltaTime)
        {
            Vector3 viewport = camera.WorldToViewportPoint(fountain.transform.position);

            if (!TryResolveEdge(viewport, safeViewport, out Vector2 edge, out float angle))
            {
                FadeItem(item, false, deltaTime);
                return;
            }

            Vector2 screenPoint = new Vector2(edge.x * Screen.width, edge.y * Screen.height);
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rootRect,
                    screenPoint,
                    uiCamera,
                    out Vector2 local))
            {
                FadeItem(item, false, deltaTime);
                return;
            }

            item.rect.anchoredPosition = local;
            item.rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            item.image.sprite = SpriteFor(fountain.Kind);
            item.image.color = ColorFor(fountain.Kind);
            item.image.rectTransform.sizeDelta = Vector2.one * iconSize;

            if (fountain.Kind == FountainKind.Life
                && _healthFraction > 0f
                && _healthFraction <= 0.4f
                && !fountain.PlayerInside)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 0.85f) * 0.08f;
                item.image.rectTransform.localScale = Vector3.one * pulse;
            }
            else
            {
                item.image.rectTransform.localScale = Vector3.one;
            }

            FadeItem(item, true, deltaTime);
        }

        private NavItem ItemAt(int index)
        {
            EnsureBuilt();

            while (_items.Count <= index)
                _items.Add(CreateItem());

            return _items[index];
        }

        private void EnsureBuilt()
        {
            if (_built || _rootRect == null)
                return;

            _built = true;

            for (int i = 0; i < PrewarmCount; i++)
                _items.Add(CreateItem());
        }

        private NavItem CreateItem()
        {
            var go = new GameObject(
                "SupplyNav",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(Image));

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(_rootRect, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * iconSize;

            Image image = go.GetComponent<Image>();
            image.sprite = _fallbackSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color32(30, 30, 29, 255);
            outline.effectDistance = new Vector2(0.8f, -0.8f);
            outline.useGraphicAlpha = true;

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            go.SetActive(false);

            return new NavItem
            {
                root = go,
                rect = rect,
                group = group,
                image = image,
            };
        }

        private void FadeItem(NavItem item, bool show, float deltaTime)
        {
            if (item == null)
                return;

            if (show && !item.root.activeSelf)
                item.root.SetActive(true);

            float target = show ? 1f : 0f;
            float step = deltaTime > 0f ? deltaTime * FadeSpeed : (show ? 1f : 0f);

            item.group.alpha = Mathf.MoveTowards(item.group.alpha, target, step);
            item.visible = show;

            if (!show && item.group.alpha <= 0.001f)
                item.root.SetActive(false);
        }

        private void HideAll(float deltaTime)
        {
            for (int i = 0; i < _items.Count; i++)
                FadeItem(_items[i], false, deltaTime);
        }

        private Camera ResolveCamera()
        {
            if (_camera != null)
                return _camera;

            _camera = Camera.main;

            return _camera;
        }

        private Sprite SpriteFor(FountainKind kind)
        {
            int index = Mathf.Clamp((int)kind, 0, _kindSprites.Length - 1);

            if (_kindSprites[index] != null)
                return _kindSprites[index];

            return _fallbackSprite;
        }

        private static Color ColorFor(FountainKind kind)
        {
            switch (kind)
            {
                case FountainKind.Earth:
                    return new Color32(208, 177, 133, 235);
                case FountainKind.Life:
                    return new Color32(126, 184, 138, 235);
                default:
                    return new Color32(133, 198, 200, 235);
            }
        }

        private static Sprite CreateDropSprite(out Texture2D texture)
        {
            const int size = 96;
            const float centerY = 34f;
            const float radius = 28f;
            const float tipY = 74f;

            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SupplyNavDrop",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float sampleY = y + 0.5f;
                float halfWidth;

                if (sampleY <= centerY)
                {
                    float dy = sampleY - centerY;
                    halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                }
                else
                {
                    float t = Mathf.Clamp01((sampleY - centerY) / (tipY - centerY));
                    halfWidth = radius * Mathf.Pow(1f - t, 0.78f);
                }

                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Abs(x + 0.5f - size * 0.5f);
                    byte alpha = (byte)(Mathf.Clamp01((halfWidth - distance + 0.5f) / 1.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);

            sprite.name = "SupplyNavDrop";

            return sprite;
        }

        private void OnHealthChanged(PlayerHealthChanged evt)
        {
            _healthFraction = evt.Max > 0f ? Mathf.Clamp01(evt.Current / evt.Max) : 0f;
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
