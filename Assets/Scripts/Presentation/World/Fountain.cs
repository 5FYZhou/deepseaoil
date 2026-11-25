using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Presentation.Lighting;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{

    public enum FountainKind
    {

        Water = 0,

        Earth = 1,

        Life = 2,
    }

    // 组合根每帧推进，非自驱 Update
    public sealed class Fountain : MonoBehaviour
    {
        private static readonly int EmissiveBoostId = Shader.PropertyToID("_EmissiveBoost");

        private static readonly Color LifeChargeColorStart = Color.white;

        private static readonly Color LifeChargeColorEnd = DefaultLightColor(FountainKind.Life);

        private const float LifeChargeIntensityStart = 0.2f;
        private const float LifeChargeIntensityEnd = 0.35f;

        private const float LifeShadowScaleMinX = 0.55f;
        private const float LifeShadowScaleMinY = 0.85f;

        [Header("种类")]
        [Tooltip("泉眼种类：Water/Earth 补弹药并吐落物，Life 只做静止回血")]
        [SerializeField] private FountainKind kind = FountainKind.Water;

        [Header("产出")]
        [Tooltip("产出的间隔（秒）。Life 泉不使用")]
        [SerializeField] private float spawnInterval = 1f;

        [Tooltip("（可选）触发区可视半径提示。玩家进出时显隐")]
        [SerializeField] private GameObject radiusView = default;

        [Header("落点")]
        [Tooltip("落点离喷泉的最大距离（世界单位）")]
        [SerializeField] private float landingRadius = 3f;

        [Tooltip("落点离喷泉的最小距离（世界单位）：避免水球落在喷泉正中心")]
        [SerializeField] private float minLandingDistance = 0.8f;

        [Header("触发区")]
        [Tooltip("自动补 Trigger 时的半径（世界单位）：1.5 覆盖以泉眼为中心的九宫格")]
        [SerializeField] private float autoTriggerRadius = 1.5f;

        [Tooltip("生命泉触发半径倍率")]
        [SerializeField] private float lifeTriggerRadiusMultiplier = 1.15f;

        [Header("变化时长")]
        [Tooltip("进入变化（蓄力上升 / 进圈变亮 / 阴影加深）的平滑时长（秒）：0 = 瞬间跳变。升慢降快，这里给大值")]
        [Range(0f, 2f)]
        [SerializeField] private float enterChangeSeconds = 0.35f;

        [Tooltip("离开变化（蓄力回落 / 退白 / 离圈变暗 / 阴影复原）的平滑时长（秒）：0 = 瞬间跳变。升慢降快，这里给小值")]
        [Range(0f, 2f)]
        [SerializeField] private float exitChangeSeconds = 0.1f;

        [Header("自发光")]
        [Tooltip("勾上才用下面的光色覆盖；不勾 = 按种类自动取色（水=青 / 土=琥珀）。生命泉不用这个：光色按蓄力从白走到绿")]
        [SerializeField] private bool overrideSelfLightColor;

        [Tooltip("光色覆盖：仅在勾上上一项时生效（Alpha 不参与判定）")]
        [ColorUsage(false, true)]
        [SerializeField] private Color selfLightOverride = new Color(1f, 0.72f, 0.35f, 1f);

        [Tooltip("自发光强度（格心峰值）；0 = 不发光。光按格扩散，泉眼因此也会照亮四周地面。生命泉不用这个：按蓄力在 0.2~0.35 之间")]
        [Range(0f, 1.5f)]
        [SerializeField] private float selfLightIntensity = 0.55f;

        [Tooltip("呼吸深度：0 = 常亮；节拍由 TileGridLightmap 的 Steady Pulse Hertz 统一定，避免各泉各闪")]
        [Range(0f, 0.5f)]
        [SerializeField] private float selfLightPulseDepth = 0.15f;

        [Header("自发光增益 (Self Emission Boost)")]
        [Tooltip("玩家进圈时白昼的增益上限（写材质的 _EmissiveBoost）；0 = 白天进圈也不亮")]
        [Range(0f, 1.5f)]
        [SerializeField] private float emissiveBoostDay = 0.2f;

        [Tooltip("玩家进圈时黄昏与夜晚的增益上限：夜里要压过暗景，通常比白天给得多")]
        [Range(0f, 1.5f)]
        [SerializeField] private float emissiveBoostNight = 0.4f;

        [Tooltip("泉眼自发光与生命泉回落的变化时长（秒）")]
        [Range(0f, 2f)]
        [SerializeField] private float emissiveFadeSeconds = 0.35f;

        [Header("阴影透明度")]
        [Tooltip("发光时 ShadowView 透明度相对初始值的最低比例：0 = 完全透明，1 = 不变")]
        [Range(0f, 1f)]
        [SerializeField] private float shadowMinAlphaScale = 0.58f;

        [Header("生命泉回血表现")]
        [Tooltip("蓄满时 View 相对初始位置的最大上浮高度（世界单位）")]
        [SerializeField] private float lifeViewLiftMax = 0.4f;

        [Tooltip("蓄力进度 0→1 的归一化表现曲线：同时驱动光色白→绿、亮度 0.2→0.35、View 的 Y 上浮与 ShadowView 缩小")]
        [SerializeField] private AnimationCurve lifeChargeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private IDropSpawner _spawner;

        private Material _emissiveMaterial;

        private Transform _viewTransform;

        private float _viewBaseLocalY;

        private float _viewLiftApplied = float.NaN;

        private float _emissiveRamp;

        private float _emissiveApplied;

        private bool _emissiveWritten;

        private bool _lifeStill;

        private float _lifeCharge;

        private float _lifeChargeTarget;

        private float _lifeDischarge;

        private bool _lifeDischarging;

        private bool _playerInside;
        private float _spawnTimer;

        private SpriteRenderer _shadowRenderer;
        private Color _shadowBaseColor;
        private bool _shadowColorCaptured;
        private float _shadowAlphaApplied = -1f;

        private Transform _shadowTransform;
        private Vector3 _shadowBaseScale;
        private Vector3 _shadowScaleApplied = new Vector3(float.NaN, float.NaN, float.NaN);

        public FountainKind Kind => kind;

        public bool PlayerInside => _playerInside;

        private float LifeDisplayProgress
            => _lifeDischarging
                ? Mathf.Lerp(1f, 0f, _lifeDischarge)
                : _lifeCharge;

        private float LifeCurveValue
            => lifeChargeCurve != null
                ? Mathf.Clamp01(lifeChargeCurve.Evaluate(LifeDisplayProgress))
                : LifeDisplayProgress;

        public float SelfLightIntensity => kind == FountainKind.Life
            ? Mathf.Lerp(LifeChargeIntensityStart, LifeChargeIntensityEnd, LifeCurveValue)
            : selfLightIntensity;

        public float SelfLightPulseDepth => selfLightPulseDepth;

        public Color SelfLightColor => kind == FountainKind.Life
            ? Color.Lerp(LifeChargeColorStart, LifeChargeColorEnd, LifeCurveValue)
            : overrideSelfLightColor ? selfLightOverride : DefaultLightColor(kind);

        public float PeakEmissiveBoost(TimeOfDayKind timeOfDay)
            => timeOfDay == TimeOfDayKind.Day ? emissiveBoostDay : emissiveBoostNight;

        public static bool GlowsInside(FountainKind fountainKind, bool playerInside, bool still)
            => playerInside && (fountainKind != FountainKind.Life || still);

        private static Color DefaultLightColor(FountainKind fountainKind)
        {
            switch (fountainKind)
            {
                case FountainKind.Earth:
                    return new Color(1f, 0.72f, 0.35f, 1f);

                case FountainKind.Life:
                    return new Color(0.35f, 1f, 0.45f, 1f);

                default:
                    return new Color(0.45f, 0.85f, 1f, 1f);
            }
        }

        // 没接线时静默不产出：日志由 CombatRoot 报
        public void Attach(IDropSpawner spawner)
        {
            _spawner = spawner;
        }

        public void SetLifeCharge(float progress01, bool still)
        {
            _lifeStill = still;
            _lifeChargeTarget = Mathf.Clamp01(progress01);

            if (_lifeChargeTarget <= 0f)
            {
                if (still) _lifeCharge = 0f;

                return;
            }

            _lifeCharge = _lifeChargeTarget;
        }

        public void NotifyLifeHealed()
        {
            _lifeDischarging = true;
            _lifeDischarge = 0f;
            _lifeCharge = 1f;
        }

        private void Awake()
        {
            EnsureTrigger();
            ResolveView();
        }

        private void OnDestroy()
        {
            if (_emissiveMaterial != null) Destroy(_emissiveMaterial);
        }

        // 属性块会顶掉 SpriteRenderer 逐渲染器的 _MainTex（泉眼整块变白），只能写实例材质
        private void ResolveView()
        {
            Transform view = transform.Find("View");

            SpriteRenderer renderer = view != null
                ? view.GetComponent<SpriteRenderer>()
                : GetComponentInChildren<SpriteRenderer>(true);

            if (renderer == null) return;

            _emissiveMaterial = renderer.material;

            _viewTransform = renderer.transform;

            _viewBaseLocalY = _viewTransform.localPosition.y;

            Transform shadow = transform.Find("ShadowView");
            if (shadow != null)
            {
                _shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                if (_shadowRenderer != null)
                {
                    _shadowBaseColor = _shadowRenderer.color;
                    _shadowColorCaptured = true;

                    _shadowTransform = shadow;
                    _shadowBaseScale = shadow.localScale;
                }
            }
        }

        private void EnsureTrigger()
        {
            Collider2D existing = GetComponent<Collider2D>();

            if (kind == FountainKind.Life)
            {
                float multiplier = SanitizeMultiplier(lifeTriggerRadiusMultiplier);

                if (existing is CircleCollider2D existingCircle)
                {
                    existingCircle.radius = ScaleTriggerRadius(existingCircle.radius, multiplier);
                }
                else if (existing == null)
                {
                    var created = gameObject.AddComponent<CircleCollider2D>();

                    created.isTrigger = true;
                    created.radius = ScaleTriggerRadius(autoTriggerRadius, multiplier);
                }
                else
                {
                    Debug.LogWarning("[Fountain] 生命泉已有一个非圆形触发体：无法按圆半径扩大 15%。", this);
                }

                if (radiusView != null)
                {
                    radiusView.transform.localScale *= multiplier;
                }

                return;
            }

            if (existing != null) return;

            var circle = gameObject.AddComponent<CircleCollider2D>();

            circle.isTrigger = true;
            circle.radius = autoTriggerRadius > 0f ? autoTriggerRadius : 1.5f;
        }

        public static float ScaleTriggerRadius(float radius, float multiplier)
        {
            return Mathf.Max(0f, radius) * SanitizeMultiplier(multiplier);
        }

        private static float SanitizeMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f) return 1.15f;

            return multiplier;
        }

        // 推进一个渲染帧；暂停时 dt 为 0，节拍冻结
        public void Tick(float deltaTime, TimeOfDayKind timeOfDay)
        {
            TickEmissive(deltaTime, timeOfDay);

            if (kind == FountainKind.Life)
            {
                TickLifeCharge(deltaTime);
                TickLifeDischarge(deltaTime);
                ApplyLifeViewLift();
                ApplyLifeShadowScale();

                return;
            }

            if (!_playerInside) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer < spawnInterval) return;

            _spawnTimer -= spawnInterval;

            SpawnOne();
        }

        private float ChangeSeconds(float current, float target)
            => target > current ? enterChangeSeconds : exitChangeSeconds;

        private void TickLifeCharge(float deltaTime)
        {
            if (_lifeChargeTarget >= _lifeCharge) return;

            float seconds = emissiveFadeSeconds > 0f
                ? emissiveFadeSeconds
                : ChangeSeconds(_lifeCharge, _lifeChargeTarget);

            _lifeCharge = Mathf.MoveTowards(
                _lifeCharge, _lifeChargeTarget, seconds > 0f ? deltaTime / seconds : 1f);
        }

        private void TickLifeDischarge(float deltaTime)
        {
            if (!_lifeDischarging) return;

            float step = exitChangeSeconds > 0f ? deltaTime / exitChangeSeconds : 1f;

            _lifeDischarge = Mathf.MoveTowards(_lifeDischarge, 1f, step);

            if (_lifeDischarge >= 1f)
            {
                _lifeDischarging = false;
                _lifeDischarge = 0f;
                _lifeCharge = 0f;
            }
        }

        private void ApplyLifeViewLift()
        {
            if (_viewTransform == null) return;

            float lift = lifeViewLiftMax * LifeCurveValue;

            if (Mathf.Approximately(lift, _viewLiftApplied)) return;

            _viewLiftApplied = lift;

            Vector3 p = _viewTransform.localPosition;

            p.y = _viewBaseLocalY + lift;

            _viewTransform.localPosition = p;
        }

        private void ApplyLifeShadowScale()
        {
            if (_shadowTransform == null) return;

            Vector3 scale = new Vector3(
                _shadowBaseScale.x * Mathf.Lerp(1f, LifeShadowScaleMinX, LifeCurveValue),
                _shadowBaseScale.y * Mathf.Lerp(1f, LifeShadowScaleMinY, LifeCurveValue),
                _shadowBaseScale.z);

            if (scale == _shadowScaleApplied) return;

            _shadowScaleApplied = scale;

            _shadowTransform.localScale = scale;
        }

        // 只写材质参数，不加灯：加灯会抢火光
        private void TickEmissive(float deltaTime, TimeOfDayKind timeOfDay)
        {
            if (_emissiveMaterial == null) return;

            float target = GlowsInside(kind, _playerInside, _lifeStill) ? 1f : 0f;
            float seconds = emissiveFadeSeconds > 0f ? emissiveFadeSeconds : 1f;
            float step = seconds > 0f ? deltaTime / seconds : 1f;

            _emissiveRamp = Mathf.MoveTowards(_emissiveRamp, target, step);

            float boost = PeakEmissiveBoost(timeOfDay) * _emissiveRamp;

            if (!_emissiveWritten || boost != _emissiveApplied)
            {
                _emissiveWritten = true;
                _emissiveApplied = boost;

                _emissiveMaterial.SetFloat(EmissiveBoostId, boost);
            }

            TickShadowAlpha();
        }

        private void TickShadowAlpha()
        {
            if (!_shadowColorCaptured || _shadowRenderer == null) return;

            float scale = Mathf.Lerp(1f, shadowMinAlphaScale, _emissiveRamp);
            float alpha = _shadowBaseColor.a * scale;

            if (Mathf.Approximately(alpha, _shadowAlphaApplied)) return;

            _shadowAlphaApplied = alpha;

            Color c = _shadowBaseColor;
            c.a = alpha;
            _shadowRenderer.color = c;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            if (_playerInside) return;

            _playerInside = true;

            if (_spawnTimer >= spawnInterval)
            {
                SpawnOne();
                _spawnTimer = 0f;
            }

            if (radiusView != null) radiusView.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.GetComponentInParent<PlayerController>() == null) return;

            _playerInside = false;

            if (radiusView != null) radiusView.SetActive(false);
        }

        public DropType OutputType
            => kind == FountainKind.Earth ? DropType.Earth : DropType.Water;

        private void SpawnOne()
        {
            if (_spawner == null) return;

            Vector2 center = transform.position;
            Vector2 landing = center + RandomLandingOffset();

            _spawner.TrySpawn(new DropSpawnRequest(OutputType, center, landing));
        }

        // 最小距离 > 最大半径时无解，靠次数上限兜底
        private Vector2 RandomLandingOffset()
        {
            const int MaxLandingAttempts = 8;

            float radius = Mathf.Max(0f, landingRadius);
            float min = Mathf.Max(0f, minLandingDistance);

            for (int i = 0; i < MaxLandingAttempts; i++)
            {
                Vector2 offset = Rng.InsideUnitCircle() * radius;

                if (offset.magnitude >= min || radius <= min) return offset;
            }

            return Rng.InsideUnitCircle() * radius;
        }
    }
}
