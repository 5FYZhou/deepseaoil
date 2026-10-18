using DeepseaOil.Logic.Events;
using UnityEngine;
// 在 DeepseaOil.Presentation.Grid 里直接写 Grid 会被解析成命名空间（CS0118），必须用别名
using GridComponent = UnityEngine.Grid;

namespace DeepseaOil.Presentation.Grid
{
    public enum TileIndicatorState
    {
        ValidAim,    // 正常有效瞄准（暖奶油白）
        InvalidAim,  // 无效/障碍/超距瞄准（软萌珊瑚粉红）
        Hidden       // 隐藏关闭
    }

    /// <summary>地格光标：底盘硬吸附 + 柔和呼吸回弹</summary>
    /// <remarks>颜色只走 MaterialPropertyBlock，不克隆材质。</remarks>
    [DisallowMultipleComponent]
    public sealed class TileCursorIndicator : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private const string DefaultColorProperty = "_Color";

        [Header("底盘核心组件绑定")]
        [Tooltip("底盘 Quad 变换组件（负责网格硬吸附与回弹微动效）")]
        [SerializeField] private Transform baseQuadTransform = default;

        [Tooltip("底盘 SpriteRenderer（材质配置为 Mat_CartoonTile）")]
        [SerializeField] private SpriteRenderer baseRenderer = default;

        [Tooltip("目标网格系统（若为空将尝试自动在场景中寻找或使用降级尺寸）")]
        [SerializeField] private GridComponent targetGrid = default;

        [Header("网格与坐标降级配置")]
        [Tooltip("当场景无 Grid 组件时的默认地格尺寸")]
        [SerializeField] private Vector2 fallbackCellSize = Vector2.one;

        [Tooltip("瓦片中心偏移量（当无 Grid 组件时生效）")]
        [SerializeField] private Vector2 fallbackCellAnchor = new Vector2(0.5f, 0.5f);

        [Header("绘本配色调色板")]
        [Tooltip("着色器主颜色属性名（自定义 Sprite 着色器通常为 _Color）")]
        [SerializeField] private string shaderColorPropertyName = "_Color";

        [Tooltip("有效瞄准时的暖奶油白底盘色")]
        [SerializeField] private Color validBaseColor = new Color(1.0f, 0.95f, 0.82f, 0.85f);

        [Tooltip("无效瞄准时的软萌珊瑚粉红")]
        [SerializeField] private Color invalidColor = new Color(1.0f, 0.46f, 0.46f, 0.85f);

        [Tooltip("底盘舒缓呼吸频率（Hz）")]
        [Range(0.1f, 2f)]
        [SerializeField] private float baseBreathFrequency = 0.3f;

        [Tooltip("底盘呼吸波谷占基准颜色的比例（0~1）")]
        [Range(0.1f, 1f)]
        [SerializeField] private float baseBreathMinAlphaRatio = 0.75f;

        [Header("吸附回弹动效 (Scale Bounce)")]
        [Tooltip("换格瞬间缩放比例（小于 1 为轻柔收缩弹回）")]
        [Range(0.7f, 1.1f)]
        [SerializeField] private float punchScale = 0.92f;

        [Tooltip("回弹动画持续时间（秒）")]
        [Range(0.04f, 0.25f)]
        [SerializeField] private float punchDuration = 0.15f;

        private MaterialPropertyBlock _basePropBlock;
        private int _colorPropId;

        // int.MinValue 哨兵：保证首帧一定触发一次吸附与回弹
        private Vector3Int _currentCellPos = new Vector3Int(int.MinValue, int.MinValue, 0);

        private Vector3 _targetCellCenter;
        private float _punchTimer;
        private TileIndicatorState _currentState = TileIndicatorState.Hidden;
        private GameRoot _root;
        private bool _canSnap;
        private bool _hasAim;
        private bool _aimAvailable;
        private bool? _seedTintOverride;

        /// <summary>驱动顺序：与战斗世界侧同帧，输入采样已先于场景根完成</summary>
        public int Order => SceneOrder.World;

        private void Awake()
        {
            _colorPropId = Shader.PropertyToID(
                string.IsNullOrEmpty(shaderColorPropertyName) ? DefaultColorProperty : shaderColorPropertyName);
            _basePropBlock = new MaterialPropertyBlock();

            if (baseRenderer != null) baseRenderer.GetPropertyBlock(_basePropBlock);

            ResolveGrid();
            ValidateReferences();
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);

            // 先订阅再收首帧 AimChanged：逻辑层已把射程夹好，指示器不再自己解算鼠标
            EventBus<AimChanged>.Subscribe(OnAimChanged);
            ApplyVisualState();
        }

        private void OnDestroy()
        {
            EventBus<AimChanged>.Unsubscribe(OnAimChanged);

            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        /// <summary>GameRoot 首个驱动帧前调一次；初始化已在 Awake</summary>
        public void Attach()
        {
        }

        public void RenderTick(float deltaTime)
        {
            if (_currentState == TileIndicatorState.Hidden) return;
            if (!isActiveAndEnabled) return;
            if (deltaTime <= 0f) return;

            UpdateBaseVisuals(deltaTime);
        }

        private void OnAimChanged(AimChanged evt)
        {
            _hasAim = evt.HasAim;
            _aimAvailable = evt.Available;

            if (_hasAim) SetTargetCell(evt.Cell);

            ApplyVisualState();
        }

        /// <summary>持种时覆盖投掷裁决色；null = 交还 AimChanged 的可用性</summary>
        public void SetSeedTint(bool? canPlant)
        {
            if (_seedTintOverride == canPlant) return;

            _seedTintOverride = canPlant;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (!_hasAim)
            {
                SetState(TileIndicatorState.Hidden);

                return;
            }

            bool valid = _seedTintOverride ?? _aimAvailable;
            SetState(valid ? TileIndicatorState.ValidAim : TileIndicatorState.InvalidAim);
        }

        /// <summary>切换三态：Valid/Invalid 显示底盘，Hidden 整体停用</summary>
        public void SetState(TileIndicatorState newState)
        {
            _currentState = newState;

            switch (_currentState)
            {
                case TileIndicatorState.ValidAim:
                case TileIndicatorState.InvalidAim:
                    gameObject.SetActive(true);
                    break;

                case TileIndicatorState.Hidden:
                    gameObject.SetActive(false);
                    break;
            }
        }

        private void SetTargetCell(Vector3Int cell)
        {
            if (!_canSnap) return;
            if (cell == _currentCellPos) return;

            Vector3 cellCenter;

            if (targetGrid != null)
            {
                cellCenter = targetGrid.GetCellCenterWorld(cell);
            }
            else
            {
                cellCenter = new Vector3(
                    (cell.x + fallbackCellAnchor.x) * fallbackCellSize.x,
                    (cell.y + fallbackCellAnchor.y) * fallbackCellSize.y,
                    0f);
            }

            _currentCellPos = cell;
            _targetCellCenter = cellCenter;

            SnapBaseToTarget();
        }

        private void SnapBaseToTarget()
        {
            if (baseQuadTransform == null) return;

            Vector3 position = baseQuadTransform.position;
            baseQuadTransform.position = new Vector3(_targetCellCenter.x, _targetCellCenter.y, position.z);

            if (punchDuration > 0f)
            {
                _punchTimer = punchDuration;
                baseQuadTransform.localScale = new Vector3(punchScale, punchScale, 1f);
            }
            else
            {
                _punchTimer = 0f;
                baseQuadTransform.localScale = Vector3.one;
            }
        }

        private void UpdateBaseVisuals(float deltaTime)
        {
            if (baseQuadTransform != null && _punchTimer > 0f)
            {
                _punchTimer -= deltaTime;

                float duration = Mathf.Max(punchDuration, 1e-4f);
                float progress = 1f - Mathf.Clamp01(_punchTimer / duration);
                float currentScale = Mathf.Lerp(punchScale, 1f, progress);

                baseQuadTransform.localScale = _punchTimer <= 0f
                    ? Vector3.one
                    : new Vector3(currentScale, currentScale, 1f);
            }

            if (baseRenderer == null || _basePropBlock == null) return;

            Color baseColor = _currentState == TileIndicatorState.ValidAim ? validBaseColor : invalidColor;
            float frequency = Mathf.Max(baseBreathFrequency, 1e-4f);
            float wave = (Mathf.Cos(Time.time * frequency * 2f * Mathf.PI) + 1f) * 0.5f;
            float ratio = Mathf.Lerp(Mathf.Clamp01(baseBreathMinAlphaRatio), 1f, wave);

            _basePropBlock.SetColor(_colorPropId, baseColor * ratio);
            baseRenderer.SetPropertyBlock(_basePropBlock);
        }

        private void ResolveGrid()
        {
            if (targetGrid != null)
            {
                _canSnap = CanUseGrid(targetGrid);
                if (!_canSnap) targetGrid = null;

                return;
            }

            GridComponent[] grids = FindObjectsOfType<GridComponent>();

            if (grids == null || grids.Length == 0)
            {
                Debug.LogWarning(
                    "[Cursor] 场景里没有 UnityEngine.Grid：改用 fallbackCellSize/fallbackCellAnchor 解算格心。", this);

                _canSnap = CanUseFallback();

                return;
            }

            targetGrid = grids[0];

            if (grids.Length > 1)
            {
                Debug.LogWarning(
                    $"[Cursor] 场景里有 {grids.Length} 个 Grid：自动取第一个，请显式接线 targetGrid。", this);
            }

            _canSnap = CanUseGrid(targetGrid);
            if (!_canSnap) targetGrid = null;
        }

        private bool CanUseGrid(GridComponent grid)
        {
            // 六边形/等轴测不是矩形格：中心换算会整体偏移，宁可停用也不静默画错
            if (grid.cellLayout != GridLayout.CellLayout.Rectangle)
            {
                Debug.LogError(
                    $"[Cursor] 只支持矩形正交格：当前 Grid.cellLayout = {grid.cellLayout}，指示器吸附已停用。", this);

                return false;
            }

            Vector3 size = grid.cellSize;

            if (!(size.x > 0f) || !(size.y > 0f) || float.IsInfinity(size.x) || float.IsInfinity(size.y))
            {
                Debug.LogError(
                    $"[Cursor] Grid.cellSize 非法（{size.x} × {size.y}）：指示器吸附已停用。", this);

                return false;
            }

            return true;
        }

        private bool CanUseFallback()
        {
            if (fallbackCellSize.x > 0f && fallbackCellSize.y > 0f &&
                !float.IsInfinity(fallbackCellSize.x) && !float.IsInfinity(fallbackCellSize.y))
            {
                return true;
            }

            Debug.LogError(
                $"[Cursor] fallbackCellSize 非法（{fallbackCellSize.x} × {fallbackCellSize.y}）：没有可用的网格解算。", this);

            return false;
        }

        private void ValidateReferences()
        {
            if (baseQuadTransform == null)
            {
                Debug.LogError("[Cursor] baseQuadTransform 未接线：底盘不会吸附格心。", this);
            }

            if (baseRenderer == null)
            {
                Debug.LogError("[Cursor] baseRenderer 未接线：底盘呼吸不会生效。", this);

                return;
            }

            if (baseRenderer.sharedMaterial == null)
            {
                Debug.LogWarning("[Cursor] BaseQuad 没有材质。", this);
            }
            else if (!baseRenderer.sharedMaterial.HasProperty(_colorPropId))
            {
                Debug.LogError(
                    $"[Cursor] 材质 {baseRenderer.sharedMaterial.name} 上没有 {shaderColorPropertyName}：" +
                    "颜色不会生效。", this);
            }

            if (baseRenderer.sprite == null)
            {
                Debug.LogWarning("[Cursor] BaseQuad 没有 Sprite：底盘不可见。", this);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.5f);

            Vector3 cellSize = targetGrid != null ? targetGrid.cellSize : (Vector3)fallbackCellSize;

            if (cellSize.x > 0f && cellSize.y > 0f)
            {
                Gizmos.DrawWireCube(_targetCellCenter, cellSize);
            }
        }
#endif
    }
}
