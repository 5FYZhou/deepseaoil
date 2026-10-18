using DeepseaOil.Presentation.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Combat
{
    /// <summary>伪 3D 侧挂武器：身体轴椭圆公转、克制透视、后背视锥层级</summary>
    /// <remarks>垂直瞄准抬升归零；LateUpdate 热路径不分配；层级/挤压只看 rawAngle；引用缺失时节流自愈。</remarks>
    // 武器根不要挂到会被 ActorMotor 翻转 localScale.x 的 Player 下，否则父级会再次镜像。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WeaponAimController : MonoBehaviour
    {
        private const string AnchorName = "WeaponAnchor";
        private const float MinAimDistance = 0.001f;
        private const float MinLiftThreshold = 0.01f;
        private const float MinForeshortenLimit = 0.70f;
        private const float ForeshortenCurvePower = 0.85f;

        [Header("核心挂载与渲染器")]
        [Tooltip("角色基准锚点；留空自愈查找 Player/WeaponAnchor，找不到则在玩家根下创建")]
        [SerializeField] private Transform characterAnchor = default;
        [Tooltip("角色主体渲染器；留空自愈到 Player/View")]
        [SerializeField] private SpriteRenderer characterRenderer = default;
        [Tooltip("武器自身渲染器；留空自愈到本节点或子节点")]
        [SerializeField] private SpriteRenderer weaponRenderer = default;
        [Tooltip("输入采样器；留空自愈查找，仍找不到则退回 Mouse.current")]
        [SerializeField] private InputProvider inputProvider = default;
        [Tooltip("瞄准相机；留空自愈取 Camera.main")]
        [SerializeField] private Camera aimCamera = default;

        [Header("身体轴椭圆滑轨")]
        [Tooltip("身体截面椭圆半径：X 身侧展开，Y 俯视进深")]
        [SerializeField] private Vector2 bodyOrbitRadius = new Vector2(0.25f, 0.2f);
        [Tooltip("锚点到胸腔的补偿；当前 WeaponAnchor 已带高度，默认 0")]
        [SerializeField] private Vector2 shoulderOffset = new Vector2(0f, 0f);
        [Tooltip("位置平滑速度；0=硬跟随，旋转始终跟手")]
        [Range(0f, 30f)]
        [SerializeField] private float smoothSpeed = 0f;

        [Header("伪 3D 透视挤压")]
        [Tooltip("正上/正下瞄准时枪身长度缩放下限；低于 0.70 会重新出现斜切")]
        [Range(0.70f, 0.95f)]
        [SerializeField] private float minForeshortenScale = 0.7f;
        [Tooltip("正上/正下瞄准时枪身厚度上限；1=不加厚")]
        [Range(1f, 1.25f)]
        [SerializeField] private float maxThicknessScale = 1f;

        [Header("层级排序与后背视锥")]
        [Tooltip("后背视锥门槛仰角；水平为 0 度，只有大于该仰角才置于身后")]
        [Range(10f, 45f)]
        [SerializeField] private float backAngleThreshold = 28f;
        [Tooltip("身后 Sorting Order 相对偏移")]
        [SerializeField] private int orderOffsetBehind = -1;
        [Tooltip("身前 Sorting Order 相对偏移")]
        [SerializeField] private int orderOffsetInFront = 1;

        [Header("贴身距离抬升")]
        [Tooltip("贴身时枪管最大上扬角；垂直瞄准会乘 cos 权重自动归零")]
        [Range(0f, 45f)]
        [SerializeField] private float maxLiftAngle = 45f;
        [Tooltip("超过该距离抬升归零")]
        [Min(0.01f)]
        [SerializeField] private float liftDistanceThreshold = 12f;
        [Tooltip("横轴=距离/阈值，纵轴=抬升强度")]
        [SerializeField] private AnimationCurve distanceLiftCurve = new AnimationCurve(
            new Keyframe(0f, 1f, -1.8f, -1.8f),
            new Keyframe(1f, 0f, 0f, 0f)
        );

        [Header("自愈")]
        [Tooltip("找不到 WeaponAnchor 时自动创建的锚点高度（相对玩家根）")]
        [SerializeField] private float autoAnchorHeight = 1.25f;
        [Tooltip("引用缺失时的重试间隔（秒）")]
        [Min(0.1f)]
        [SerializeField] private float selfHealInterval = 0.5f;

        private Transform _cachedTransform;
        private Camera _cachedCamera;
        private Vector3 _currentVelocity;
        private Vector3 _baseScale;
        private PlayerController _player;
        private Transform _autoAnchor;
        private float _nextSelfHealTime;
        private bool _missingWarned;

        private void Awake()
        {
            _cachedTransform = transform;
            _baseScale = _cachedTransform.localScale;
            _cachedCamera = aimCamera != null ? aimCamera : Camera.main;
            SelfHeal();
        }

        private void Start()
        {
            SelfHeal();
            WarnIfMissing();
        }

        private void LateUpdate()
        {
            if (characterAnchor == null || _cachedCamera == null
                || weaponRenderer == null || characterRenderer == null)
            {
                TrySelfHeal();
                if (characterAnchor == null || _cachedCamera == null) return;
            }

            if (!TryGetAimWorld(out Vector2 aimWorld)) return;

            AimSettings settings = BuildSettings();
            AimSolution solution = Solve(characterAnchor.position, aimWorld, in settings);

            ApplyPose(in solution);
        }

        private AimSettings BuildSettings()
        {
            return new AimSettings(
                shoulderOffset,
                bodyOrbitRadius,
                minForeshortenScale,
                maxThicknessScale,
                backAngleThreshold,
                maxLiftAngle,
                liftDistanceThreshold,
                distanceLiftCurve
            );
        }

        private void TrySelfHeal()
        {
            // 只在引用缺失时走 Find/Camera.main；齐全后 LateUpdate 不再查。
            if (Time.unscaledTime < _nextSelfHealTime) return;

            _nextSelfHealTime = Time.unscaledTime + selfHealInterval;
            SelfHeal();
        }

        private void SelfHeal()
        {
            if (_cachedTransform == null) _cachedTransform = transform;

            if (weaponRenderer == null)
            {
                weaponRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (aimCamera != null)
            {
                _cachedCamera = aimCamera;
            }
            else if (_cachedCamera == null)
            {
                _cachedCamera = Camera.main;
            }

            if (_player == null) _player = GetComponentInParent<PlayerController>();
            if (_player == null) _player = FindObjectOfType<PlayerController>();

            if (characterAnchor == null && _player != null)
            {
                characterAnchor = ResolveAnchor(_player);
            }

            if (characterRenderer == null && _player != null)
            {
                characterRenderer = ResolveCharacterRenderer(_player);
            }

            if (inputProvider == null)
            {
                inputProvider = FindObjectOfType<InputProvider>();
            }
        }

        private Transform ResolveAnchor(PlayerController player)
        {
            Transform[] transforms = player.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == AnchorName) return transforms[i];
            }

            if (_autoAnchor == null)
            {
                var go = new GameObject(AnchorName);
                _autoAnchor = go.transform;
                _autoAnchor.SetParent(player.transform, false);
                _autoAnchor.localPosition = new Vector3(0f, autoAnchorHeight, 0f);
            }

            return _autoAnchor;
        }

        private static SpriteRenderer ResolveCharacterRenderer(PlayerController player)
        {
            if (player.Visual != null && player.Visual.Renderer != null)
            {
                return player.Visual.Renderer;
            }

            Transform view = player.transform.Find("View");

            return view != null ? view.GetComponent<SpriteRenderer>() : null;
        }

        private void WarnIfMissing()
        {
            if (_missingWarned) return;

            if (characterAnchor == null)
            {
                _missingWarned = true;
                Debug.LogWarning("WeaponAimController 未能自愈到 Player/WeaponAnchor：请把武器放到场景根或手动接线。", this);
            }
            else if (_cachedCamera == null)
            {
                _missingWarned = true;
                Debug.LogWarning("WeaponAimController 找不到相机：请接线 aimCamera 或给主相机加 MainCamera 标签。", this);
            }
        }

        private bool TryGetAimWorld(out Vector2 world)
        {
            world = default;

            Vector2 screen;

            if (inputProvider != null)
            {
                if (!inputProvider.IsInputEnabled) return false;

                screen = inputProvider.AimScreen;
            }
            else
            {
                // 工程只启用 New Input System；Input.mousePosition 会直接抛异常。
                Mouse mouse = Mouse.current;

                if (mouse == null) return false;

                screen = mouse.position.ReadValue();
            }

            Vector3 screenPoint = new Vector3(screen.x, screen.y, -_cachedCamera.transform.position.z);
            Vector3 point = _cachedCamera.ScreenToWorldPoint(screenPoint);

            world = new Vector2(point.x, point.y);

            return true;
        }

        private void ApplyPose(in AimSolution solution)
        {
            Vector3 targetPosition = new Vector3(
                solution.Position.x,
                solution.Position.y,
                _cachedTransform.position.z
            );

            if (smoothSpeed > 0f)
            {
                _cachedTransform.position = Vector3.SmoothDamp(
                    _cachedTransform.position,
                    targetPosition,
                    ref _currentVelocity,
                    1f / smoothSpeed
                );
            }
            else
            {
                _cachedTransform.position = targetPosition;
            }

            _cachedTransform.rotation = Quaternion.Euler(0f, 0f, solution.Angle);

            // Pivot 在握把：X 压缩才会让枪管向握把回缩，而不是整枪两头缩。
            _cachedTransform.localScale = new Vector3(
                _baseScale.x * solution.Scale.x,
                _baseScale.y * solution.Scale.y,
                _baseScale.z
            );

            if (weaponRenderer != null && weaponRenderer.flipY != solution.FacingLeft)
            {
                weaponRenderer.flipY = solution.FacingLeft;
            }

            if (weaponRenderer == null || characterRenderer == null) return;

            int layer = characterRenderer.sortingLayerID;
            int order = characterRenderer.sortingOrder
                + (solution.IsBehind ? orderOffsetBehind : orderOffsetInFront);

            if (weaponRenderer.sortingLayerID != layer)
            {
                weaponRenderer.sortingLayerID = layer;
            }

            if (weaponRenderer.sortingOrder != order)
            {
                weaponRenderer.sortingOrder = order;
            }
        }

        /// <summary>纯数学解算：身体轴椭圆位置、rawAngle 层级、透视挤压与视觉抬升</summary>
        public static AimSolution Solve(Vector2 characterPosition, Vector2 aimTarget, in AimSettings settings)
        {
            Vector2 shoulderOrigin = characterPosition + settings.ShoulderOffset;
            Vector2 toTarget = aimTarget - shoulderOrigin;
            float distance = toTarget.magnitude;

            // 锚点重合时方向无意义；固定右侧并保留最小距离，避免 NaN。
            if (distance < MinAimDistance)
            {
                toTarget = Vector2.right;
                distance = MinAimDistance;
            }

            float rawAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float rawRadians = rawAngle * Mathf.Deg2Rad;
            bool facingLeft = Mathf.Abs(rawAngle) > 90f;

            float cosFactor = Mathf.Abs(Mathf.Cos(rawRadians));
            float sinFactor = Mathf.Abs(Mathf.Sin(rawRadians));

            float threshold = Mathf.Clamp(settings.BackAngleThreshold, 0f, 90f);

            // 层级只看 rawAngle；抬升角绝不能参与这里。
            bool isBehind = rawAngle > threshold && rawAngle < 180f - threshold;

            Vector2 orbit = new Vector2(
                Mathf.Cos(rawRadians) * settings.BodyOrbitRadius.x,
                Mathf.Sin(rawRadians) * settings.BodyOrbitRadius.y
            );

            Vector2 position = shoulderOrigin + orbit;

            float normalizedDistance = Mathf.Clamp01(
                distance / Mathf.Max(settings.LiftDistanceThreshold, MinLiftThreshold)
            );

            float liftFactor = settings.LiftCurve != null
                ? settings.LiftCurve.Evaluate(normalizedDistance)
                : 0f;

            // 垂直瞄准 cosFactor=0：抬升必须归零，否则 ±90° 会固定歪。
            float liftAngle = liftFactor * settings.MaxLiftAngle * cosFactor;
            float signedLift = facingLeft ? -liftAngle : liftAngle;
            float visualAngle = Mathf.DeltaAngle(0f, rawAngle + signedLift);

            // 0.85 次幂让 45° 附近的长度收缩更缓，避免宽高比突变造成斜切。
            float smoothCos = Mathf.Pow(cosFactor, ForeshortenCurvePower);

            float lengthScale = Mathf.Lerp(
                Mathf.Clamp(settings.MinForeshortenScale, MinForeshortenLimit, 1f),
                1f,
                smoothCos
            );

            float thicknessScale = Mathf.Lerp(
                1f,
                Mathf.Max(1f, settings.MaxThicknessScale),
                sinFactor
            );

            return new AimSolution(
                position,
                visualAngle,
                distance,
                liftAngle,
                facingLeft,
                isBehind,
                new Vector2(lengthScale, thicknessScale)
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (characterAnchor == null) return;

            Vector3 origin = characterAnchor.position + (Vector3)shoulderOffset;
            float threshold = Mathf.Clamp(backAngleThreshold, 0f, 90f);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            Gizmos.DrawRay(origin, Direction(threshold) * 1.5f);
            Gizmos.DrawRay(origin, Direction(180f - threshold) * 1.5f);

            Gizmos.color = Color.yellow;
            DrawOrbit(origin);

            Gizmos.color = new Color(0f, 1f, 0.8f, 0.25f);
            Gizmos.DrawWireSphere(origin, liftDistanceThreshold);
        }

        private static Vector3 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        private void DrawOrbit(Vector3 center)
        {
            const int segments = 32;

            Vector3 prevPoint = center + new Vector3(bodyOrbitRadius.x, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;

                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * bodyOrbitRadius.x,
                    Mathf.Sin(angle) * bodyOrbitRadius.y,
                    0f
                );

                Gizmos.DrawLine(prevPoint, nextPoint);

                prevPoint = nextPoint;
            }
        }
#endif

        public readonly struct AimSettings
        {
            public readonly Vector2 ShoulderOffset;
            public readonly Vector2 BodyOrbitRadius;
            public readonly float MinForeshortenScale;
            public readonly float MaxThicknessScale;
            public readonly float BackAngleThreshold;
            public readonly float MaxLiftAngle;
            public readonly float LiftDistanceThreshold;
            public readonly AnimationCurve LiftCurve;

            public AimSettings(
                Vector2 shoulderOffset,
                Vector2 bodyOrbitRadius,
                float minForeshortenScale,
                float maxThicknessScale,
                float backAngleThreshold,
                float maxLiftAngle,
                float liftDistanceThreshold,
                AnimationCurve liftCurve)
            {
                ShoulderOffset = shoulderOffset;
                BodyOrbitRadius = bodyOrbitRadius;
                MinForeshortenScale = minForeshortenScale;
                MaxThicknessScale = maxThicknessScale;
                BackAngleThreshold = backAngleThreshold;
                MaxLiftAngle = maxLiftAngle;
                LiftDistanceThreshold = liftDistanceThreshold;
                LiftCurve = liftCurve;
            }
        }

        public readonly struct AimSolution
        {
            public readonly Vector2 Position;
            public readonly float Angle;
            public readonly float Distance;
            public readonly float LiftAngle;
            public readonly bool FacingLeft;
            public readonly bool IsBehind;
            public readonly Vector2 Scale;

            public AimSolution(
                Vector2 position,
                float angle,
                float distance,
                float liftAngle,
                bool facingLeft,
                bool isBehind,
                Vector2 scale)
            {
                Position = position;
                Angle = angle;
                Distance = distance;
                LiftAngle = liftAngle;
                FacingLeft = facingLeft;
                IsBehind = isBehind;
                Scale = scale;
            }
        }
    }
}
