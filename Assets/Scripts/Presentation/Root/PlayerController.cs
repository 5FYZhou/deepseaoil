using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.Visual;
using cfg.dso;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>玩家组合根：组装执行器/配置/活动区域/输入缓冲</summary>
    /// <remarks>Push 先于 Tick，否则按下沿滞后一帧；WorldInfo 与缓冲快照必须共用同一份方向，否则斜向快 √2 倍。</remarks>
    public sealed class PlayerController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked, IManagedActor, IYSortAnchor
    {
        public int Order => SceneOrder.Player;

        [SerializeField] private PlayerMotor motor = default;
        [SerializeField] private InputProvider inputProvider = default;

        [Tooltip("瞄准用的相机。留空取 Camera.main（战斗场景里就是主相机，所以通常不用拖）")]
        [SerializeField] private Camera aimCamera = default;

        [Tooltip("地图活动区域：拖入覆盖可行走区域的 BoxCollider2D。不接则不钳位（不报错，调试面板会显示未接线）")]
        [SerializeField] private BoxCollider2D boundsArea = default;

        [Tooltip("视觉适配器，必须挂在子节点 View 上（Root 只放物理与逻辑）。留空则 Awake 自愈")]
        [SerializeField] private ActorAnimationView animView = default;

        private float _dustTimer;

        private const float DirectionEpsilon = 1e-6f;

        private const float OctantRadians = 2f * Mathf.PI / 8f;

        private static readonly Dictionary<int, Vector2> Snapped = BuildSnappedTable();

        private InputBuffer _buffer;

        private WorldInfo _world;

        private BoundsArea _bounds;

        private PlayerSpec _spec;

        /// <summary>瞄准平面深度，世界单位，Attach 取一次</summary>
        private float _cameraPlaneDepth = 100f;

        private ActorAnimationView _animView;

        private Vector2 _spawnPoint;

        private GameRoot _root;

        public PlayerLogic Logic { get; private set; }

        public Vector2 Position => motor == null ? Vector2.zero : motor.Position;

        // 锚点取物理体位置：View 的 transform y 会被受击抖动与伪高度改写
        Vector2 IYSortAnchor.SortAnchor => Position;

        public ActorAnimationView Visual => _animView;

        public WorldInfo World => _world;

        /// <summary>输入采样器，战斗组合据此读播种键与移动输入</summary>
        public InputProvider Input => inputProvider;

        /// <summary>引擎回读速度，滞后一物理步</summary>
        public Vector2 EngineVelocity => motor == null ? Vector2.zero : motor.EngineVelocity;

        /// <summary>输入吸附到 8 向并归一化</summary>
        public static Vector2 SnapMoveToEightDirections(Vector2 move, bool snapToEightDirections)
        {
            if (move.sqrMagnitude <= DirectionEpsilon) return Vector2.zero;

            // 关掉吸附也不能原样放行：(1,1) 模长 √2，会写出快 41% 的速度。
            if (!snapToEightDirections) return move.normalized;

            // Round 把 ±22.5° 内输入归到最近一档，无需死区；查表而非 Cos/Sin 算值，让同一方向逐位相同。
            int octant = Mathf.RoundToInt(Mathf.Atan2(move.y, move.x) / OctantRadians);
            octant %= 8;
            if (octant < 0) octant += 8;

            return Snapped[octant];
        }

        private static Dictionary<int, Vector2> BuildSnappedTable()
        {
            var table = new Dictionary<int, Vector2>(8);

            for (int octant = 0; octant < 8; octant++)
            {
                float radians = octant * OctantRadians;
                table[octant] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }

            return table;
        }

        /// <summary>回出生点并满血</summary>
        /// <remarks>出生点越界时钳回。</remarks>
        public void RespawnToSpawn()
        {
            if (Logic == null) return;

            Vector2 position = _spawnPoint;

            if (_bounds.TryClamp(position, out Vector2 clamped)) position = clamped;

            Logic.RespawnTo(position);

            // 死亡触发器未必已被消费，不清会补播一次倒地
            _animView?.ResetToDefault();
        }

        /// <summary>已死、进入重生等待：世界侧裁决死亡时调</summary>
        public void OnDeathTriggered()
        {
            _animView?.TriggerDie();
        }

        /// <summary>玩家受击表现：真身刷白，到点由 ActorTintDriver 还原</summary>
        public void OnHurt(in Damage damage)
        {
            SpriteRenderer body = _animView != null ? _animView.Renderer : null;

            if (body == null) return;

            EffectContext ctx = EffectContext.OnTarget(body.transform);
            ctx.Duration = 0.12f;
            ctx.Tint = Color.white;

            EffectModule.Play(EffectId.Flash, in ctx);
        }

        /// <summary>本类自愈装配视觉子节点 View</summary>
        /// <remarks>兜底造出的 View 照抄 Root 渲染器的 sprite/颜色/排序层，老预制体自愈后视觉不跳变。</remarks>
        private void EnsureAnimationView()
        {
            if (animView == null) animView = GetComponentInChildren<ActorAnimationView>(true);

            if (animView == null)
            {
                var view = new GameObject("View");

                view.transform.SetParent(transform, false);

                var sprite = view.AddComponent<SpriteRenderer>();

                // 老拓扑的渲染器在 Root 上：参数照搬，免得自愈之后玩家变白块
                var legacy = GetComponent<SpriteRenderer>();

                if (legacy != null)
                {
                    sprite.sprite = legacy.sprite;
                    sprite.color = legacy.color;
                    sprite.sortingLayerID = legacy.sortingLayerID;
                    sprite.sortingOrder = legacy.sortingOrder;
                }

                animView = view.AddComponent<ActorAnimationView>();
            }

            // 两个字段各司其职：animView 是序列化接线，_animView 是运行期缓存
            _animView = animView;
        }

        private void Awake()
        {
            if (motor == null
                || inputProvider == null)
            {
                Debug.LogError("PlayerController 引用未接线（motor / inputProvider），已停用。", this);
                enabled = false;
                return;
            }

            EnsureAnimationView();

            _bounds = ReadBounds();
            _world = new WorldInfo(Vector2.zero, in _bounds);
            _spawnPoint = motor.Position;

            if (!_bounds.IsValid)
            {
                Debug.LogWarning(
                    "PlayerController.boundsArea 未接线、被停用或尺寸为 0：玩家不会被限制在地图边界内。",
                    this
                    );
            }
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            // 用 Start 里抓住的引用：销毁期再问 GameRoot.Instance 可能当场造一个新的出来
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

        /// <summary>装配玩家逻辑</summary>
        /// <remarks>读表必须放这里：ConfigModule 由 GameRoot.Awake 装配，组件 Awake 顺序不保证，写在 Awake 里会抛。</remarks>
        public void Attach()
        {
            if (Logic != null || motor == null) return;

            _spec = ConfigModule.GetPlayer();

            // 缓冲容量取容量参数与各输入窗口的较大者：小于窗口时窗口内按下会被挤出历史。
            _buffer = new InputBuffer(
                Mathf.Max(_spec.InputBufferSeconds, _spec.DashBufferSeconds),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            _cameraPlaneDepth = _spec.CameraPlaneDepth;

            Logic = new PlayerLogic(motor, _spec, _buffer);
        }

        /// <summary>物理帧</summary>
        public void FixedTick(float deltaTime)
        {
            if (Logic == null) return;

            if (!Logic.IsAlive) return;

            InputSnapshot raw = inputProvider.ConsumeSnapshot();

            Vector2 move = SnapMoveToEightDirections(raw.Move, _spec.SnapToEightDirections);

            // 归一化后的方向要写回快照：WorldInfo 与 InputBuffer 必须是同一份，否则 MoveGroup 与 PlayerLogic 的最近朝向会看到未处理值。
            var snapshot = new InputSnapshot(move, raw.DashPressed);

            _buffer.Push(in snapshot, Time.fixedTime);

            _world = new WorldInfo(move, in _bounds);

            Logic.FixedTick(
                new LogicContext(
                    Time.fixedTime,
                    Time.fixedDeltaTime,
                    in _world,
                    in snapshot
                )
            );

            // 保险丝：只在真越界时写位置，避免每帧打断刚体位置积分。
            if (_world.Bounds.TryClamp(motor.Position, out Vector2 clamped))
            {
                motor.SetPosition(clamped);
            }
        }

        /// <summary>渲染帧：瞄准与开火意图</summary>

        public void RenderTick(float deltaTime)
        {
            if (Logic == null) return;

            // 运动学快照每渲染帧推一次（含暂停帧）：插值出来的位置比物理帧平滑，动画不该跟着 50Hz 跳。
            _animView?.SetMotion(_world.MoveDirection, Logic.Motor.Facing, EngineVelocity.magnitude);

            TickFootDust(deltaTime);

            if (inputProvider == null || !inputProvider.IsInputEnabled)
            {
                Logic.ClearAim();
                return;
            }

            Camera camera = aimCamera != null ? aimCamera : Camera.main;

            if (camera == null || inputProvider == null) return;

            float now = Time.time;

            Logic.UpdateAim(AimWorldPoint(camera), now);

            // 动作表无攻击动作：由 InputProvider 直读指针产出。主攻击=水球（耗弹药），副攻击=土球（不耗）。
            // 只有世界侧采纳了才播出手动作：否则空放/射程外也会抬手。
            if (inputProvider.AttackPressedThisFrame && Logic.RequestThrow(BallType.Water, now))
            {
                _animView?.Trigger(PlayerAnimHashes.TriggerThrow);
            }

            if (inputProvider.AltAttackPressedThisFrame && Logic.RequestThrow(BallType.Earth, now))
            {
                _animView?.Trigger(PlayerAnimHashes.TriggerThrow);
            }
        }

        // 扬尘节拍；dt<=0（暂停）冻结，与输入是否启用无关
        private void TickFootDust(float dt)
        {
            if (dt <= 0f) return;

            VisualFeelTuning feel = ConfigModule.VisualFeel;

            float speed = EngineVelocity.magnitude;

            if (speed < feel.dustSpeedThreshold)
            {
                // 停下立刻归零：下次起步即发射，不用等半个节拍
                _dustTimer = 0f;

                return;
            }

            _dustTimer -= dt;

            if (_dustTimer > 0f) return;

            // 速度越快节拍越密：满速时取 dustInterval
            float ratio = Mathf.Clamp(speed / feel.dustReferenceSpeed, 0.5f, 2f);

            _dustTimer = feel.dustInterval / ratio;

            EffectContext ctx = EffectContext.At(Position);
            ctx.Value = Mathf.Clamp01(speed / feel.dustReferenceSpeed);

            EffectModule.Play(EffectId.Dust, in ctx);
        }

        private Vector2 AimWorldPoint(Camera camera)
        {
            Vector2 screen = inputProvider.AimScreen;

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _cameraPlaneDepth));

            return new Vector2(world.x, world.y);
        }

        /// <summary>活动区域碰撞体折算成纯数据矩形</summary>
        private BoundsArea ReadBounds()
        {
            if (boundsArea == null || !boundsArea.enabled) return default;

            Bounds b = boundsArea.bounds;

            return new BoundsArea(b.min, b.max);
        }
    }
}
