using DeepseaOil.Presentation.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace DeepseaOil.Presentation.Combat
{
    /// <summary>左右侧挂武器：按准心水平方向换边，按距离抬升枪管，并同步翻转与层级</summary>
    /// <remarks>LateUpdate 无堆分配；引用缺失时自愈；输入优先 InputProvider，未接时退回 Mouse.current。</remarks>
    // 武器根不要挂在会被 ActorMotor 翻转 localScale.x 的 Player 根下，否则父级会再次镜像。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class WeaponAimController : MonoBehaviour
    {
        private const string AnchorName = "WeaponAnchor";
        private const float MinAimDistance = 0.001f;
        private const float MinLiftThreshold = 0.01f;

        [Header("挂载与引用")]
        [Tooltip("侧挂参考点；留空自愈查找 Player/WeaponAnchor，找不到则在玩家根下创建")]
        [SerializeField] private Transform characterAnchor = default;
        [Tooltip("武器 SpriteRenderer；留空自愈到本节点或子节点")]
        [SerializeField] private SpriteRenderer weaponRenderer = default;
        [Tooltip("输入采样器；留空自愈查找，仍找不到则退回 Mouse.current")]
        [SerializeField] private InputProvider inputProvider = default;
        [Tooltip("瞄准相机；留空自愈取 Camera.main")]
        [SerializeField] private Camera aimCamera = default;

        [Header("左右侧挂")]
        [Tooltip("X=侧挂水平距离，Y=相对锚点的垂直偏移（世界单位）")]
        [FormerlySerializedAs("orbitRadius")]
        [SerializeField] private Vector2 sideOffset = new Vector2(0.6f, 0.4f);
        [Tooltip("位置平滑速度；0=硬跟随，旋转始终跟手")]
        [Range(0f, 30f)]
        [SerializeField] private float smoothSpeed = 0f;

        [Header("距离抬升")]
        [Tooltip("近距离时枪管最大抬升角（度）")]
        [Range(0f, 60f)]
        [SerializeField] private float maxLiftAngle = 25f;
        [Tooltip("超过该距离抬升归零（世界单位）")]
        [Min(0.01f)]
        [SerializeField] private float liftDistanceThreshold = 3.0f;
        [Tooltip("横轴=距离/阈值，纵轴=抬升强度")]
        [SerializeField] private AnimationCurve distanceLiftCurve = new AnimationCurve(
            new Keyframe(0f, 1f, -1.8f, -1.8f),
            new Keyframe(1f, 0f, 0f, 0f)
        );

        [Header("层级排序")]
        [Tooltip("玩家主体 SpriteRenderer；留空自愈到 Player/View")]
        [SerializeField] private SpriteRenderer characterRenderer = default;
        [Tooltip("准心在锚点上方时的 order 偏移（人后）")]
        [SerializeField] private int orderOffsetBehind = -1;
        [Tooltip("准心在锚点下方时的 order 偏移（人前）")]
        [SerializeField] private int orderOffsetInFront = 1;

        [Header("自愈")]
        [Tooltip("找不到 WeaponAnchor 时自动创建的锚点高度（相对玩家根）")]
        [SerializeField] private float autoAnchorHeight = 1.25f;
        [Tooltip("引用缺失时的重试间隔（秒）")]
        [Min(0.1f)]
        [SerializeField] private float selfHealInterval = 0.5f;

        private Transform _cachedTransform;
        private Camera _cachedCamera;
        private Vector3 _currentVelocity;
        private PlayerController _player;
        private Transform _autoAnchor;
        private float _nextSelfHealTime;
        private bool _missingWarned;

        private void Awake()
        {
            _cachedTransform = transform;
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
            if (characterAnchor == null || _cachedCamera == null)
            {
                TrySelfHeal();
                if (characterAnchor == null || _cachedCamera == null) return;
            }

            if (!TryGetAimWorld(out Vector2 aimWorld)) return;

            AimSolution solution = Solve(
                characterAnchor.position,
                aimWorld,
                sideOffset,
                maxLiftAngle,
                liftDistanceThreshold,
                distanceLiftCurve
            );

            ApplyPose(in solution);
        }

        public void SetCharacterAnchor(Transform anchor, SpriteRenderer charRenderer = null)
        {
            characterAnchor = anchor;

            if (charRenderer != null)
            {
                characterRenderer = charRenderer;
            }
        }

        private void TrySelfHeal()
        {
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

            if (weaponRenderer == null) return;

            if (weaponRenderer.flipY != solution.FacingLeft)
            {
                weaponRenderer.flipY = solution.FacingLeft;
            }

            if (characterRenderer == null) return;

            int layer = characterRenderer.sortingLayerID;
            int order = characterRenderer.sortingOrder
                + (solution.AimingUpwards ? orderOffsetBehind : orderOffsetInFront);

            if (weaponRenderer.sortingLayerID != layer)
            {
                weaponRenderer.sortingLayerID = layer;
            }

            if (weaponRenderer.sortingOrder != order)
            {
                weaponRenderer.sortingOrder = order;
            }
        }

        /// <summary>纯数学解算：侧挂位置、抬升角、翻转与上下半球</summary>
        public static AimSolution Solve(
            Vector2 origin,
            Vector2 aimTarget,
            Vector2 sideOffset,
            float maxLiftAngle,
            float liftDistanceThreshold,
            AnimationCurve distanceLiftCurve)
        {
            Vector2 toTarget = aimTarget - origin;
            float distance = toTarget.magnitude;

            // 锚点重合时方向无意义；固定右侧并保留最小距离，避免除零与 NaN。
            if (distance < MinAimDistance)
            {
                toTarget = Vector2.right;
                distance = MinAimDistance;
            }

            float normalizedDistance = Mathf.Clamp01(
                distance / Mathf.Max(liftDistanceThreshold, MinLiftThreshold)
            );

            float liftFactor = distanceLiftCurve != null
                ? distanceLiftCurve.Evaluate(normalizedDistance)
                : 0f;

            float liftAngle = liftFactor * maxLiftAngle;

            bool facingLeft = toTarget.x < 0f;
            bool aimingUpwards = toTarget.y > 0f;

            // 左挂基角 180°，抬升取负，保证枪管仍朝上。
            float baseAngle = facingLeft ? 180f : 0f;
            float signedLift = facingLeft ? -liftAngle : liftAngle;
            float finalAngle = baseAngle + signedLift;

            float sideX = Mathf.Abs(sideOffset.x);

            Vector2 side = new Vector2(
                facingLeft ? -sideX : sideX,
                sideOffset.y
            );

            return new AimSolution(
                origin + side,
                finalAngle,
                distance,
                liftAngle,
                facingLeft,
                aimingUpwards
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (characterAnchor == null) return;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(characterAnchor.position, liftDistanceThreshold);

            Gizmos.color = Color.yellow;

            float sideX = Mathf.Abs(sideOffset.x);

            Vector3 left = characterAnchor.position + new Vector3(-sideX, sideOffset.y, 0f);
            Vector3 right = characterAnchor.position + new Vector3(sideX, sideOffset.y, 0f);

            Gizmos.DrawLine(left, right);
            Gizmos.DrawWireSphere(left, 0.05f);
            Gizmos.DrawWireSphere(right, 0.05f);
        }
#endif

        public readonly struct AimSolution
        {
            public readonly Vector2 Position;
            public readonly float Angle;
            public readonly float Distance;
            public readonly float LiftAngle;
            public readonly bool FacingLeft;
            public readonly bool AimingUpwards;

            public AimSolution(
                Vector2 position,
                float angle,
                float distance,
                float liftAngle,
                bool facingLeft,
                bool aimingUpwards)
            {
                Position = position;
                Angle = angle;
                Distance = distance;
                LiftAngle = liftAngle;
                FacingLeft = facingLeft;
                AimingUpwards = aimingUpwards;
            }
        }
    }
}
