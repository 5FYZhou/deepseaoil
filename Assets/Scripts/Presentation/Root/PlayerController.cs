using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.Visual;
using cfg.demo;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>玩家组合根：组装执行器、配置、活动区域与输入缓冲，并由 <c>GameRoot</c> 每个物理帧驱动一次 <see cref="PlayerLogic"/>。</summary>
    /// <remarks>全部环境事实只在本类组装一次。同一物理帧内 <c>InputBuffer.Push</c> 必须先于 <c>Tick</c>，否则按下沿会滞后一帧；顺序固定：消费快照 → 吸附到 8 向并归一化 → 以<b>同一份</b>归一化快照推缓冲 → 组装 <c>WorldInfo</c> → <c>Logic.FixedTick</c> → 边界钳位（保险丝，防高速冲出地图；正常阻挡由刚体碰撞解算）。
    /// <b>归一化只在这里做一次</b>：<c>WorldInfo.MoveDirection</c> 与 <c>InputBuffer</c> 里的快照必须是同一份已归一化方向 —— 两处各留一份方向真值，正是"斜向快 √2 倍"与"冲刺方向不一致"这类不报错、只错手感的缺陷的来源。
    /// <b>物理帧只有一个发起者</b>（<c>GameRoot</c>），顺序由 <see cref="Order"/> 写死：玩家侧（<c>-100</c>）先于世界侧（<c>0</c>）。<b>两个帧相位各有一件事</b>：物理帧 = <see cref="FixedTick"/>（输入 → 逻辑 → 提交速度），渲染帧 = <see cref="RenderTick"/>（瞄准 ＋ 开火意图）；"屏幕 → 世界"只有表现层做得了，吸附到哪一格由逻辑层算（<c>PlayerLogic.UpdateAim</c>）。</remarks>
    public sealed class PlayerController : MonoBehaviour, ISceneRoot, IPhysicsTicked, IRenderTicked, IManagedActor
    {
        /// <summary>驱动顺序：玩家侧必须早于世界侧（先提交速度、先读输入）。</summary>
        public int Order => SceneOrder.Player;

        [SerializeField] private PlayerMotor motor = default;
        [SerializeField] private InputProvider inputProvider = default;

        [Tooltip("瞄准用的相机。留空取 Camera.main（战斗场景里就是主相机，所以通常不用拖）")]
        [SerializeField] private Camera aimCamera = default;

        [Tooltip("地图活动区域：拖入覆盖可行走区域的 BoxCollider2D。不接则不钳位（不报错，调试面板会显示未接线）")]
        [SerializeField] private BoxCollider2D boundsArea = default;

        /// <summary>方向判零的容差：摇杆漂移与浮点残渣不该让角色每帧微动。</summary>
        private const float DirectionEpsilon = 1e-6f;

        /// <summary>8 向吸附的一档（弧度）。45° 一档共 8 档。</summary>
        private const float OctantRadians = 2f * Mathf.PI / 8f;

        /// <summary>吸附到 8 向时的一族单位方向，键为"档位序号化的弧度"，避免浮点直接比 key。</summary>
        private static readonly Dictionary<int, Vector2> Snapped = BuildSnappedTable();

        private InputBuffer _buffer;
        private WorldInfo _world;
        private BoundsArea _bounds;

        /// <summary>玩家取值边界（<c>Attach</c> 时取一次）；配置对象不往表现层走：要哪条数就问 <see cref="PlayerSpec"/> 要哪条语义。</summary>
        private PlayerSpec _spec;

        /// <summary>瞄准平面深度（世界单位，正交相机下够大即可）；<c>Attach</c> 时取一次。</summary>
        private float _cameraPlaneDepth = 100f;

        private SpriteRenderer _body;

        private Vector2 _spawnPoint;

        private GameRoot _root;

        public PlayerLogic Logic { get; private set; }

        /// <summary>物理体位置（世界侧判接触、算重生点用物理体而不是 <c>transform</c>）。</summary>
        public Vector2 Position => motor == null ? Vector2.zero : motor.Position;
        public WorldInfo World => _world;

        /// <summary>引擎回读速度，与逻辑层的"提交后预期"对照；它滞后一个物理步。</summary>
        public Vector2 EngineVelocity => motor == null ? Vector2.zero : motor.EngineVelocity;

        /// <summary>把原始输入吸附到 8 向并归一化：键盘同时按两个轴会得到模长 √2，摇杆则是任意角度。</summary>
        /// <returns>零输入 → <c>Vector2.zero</c>；否则 → <b>模长恒为 1</b> 的方向（<c>WorldInfo.MoveDirection</c> 与逻辑层都依赖这条契约；因此"吸附到 45° 的一档"与"抹掉摇杆的模拟幅度"是同一件事 —— 轻推摇杆与推满是同一个速度）。静态纯函数，EditMode 测试能直接调它。</returns>
        public static Vector2 SnapMoveToEightDirections(Vector2 move, bool snapToEightDirections)
        {
            if (move.sqrMagnitude <= DirectionEpsilon) return Vector2.zero;

            // 关掉吸附时也不能原样放行：斜向的 (1,1) 模长是 √2，会当帧写出快 41% 的速度。
            if (!snapToEightDirections) return move.normalized;

            // 45° 一档共 8 档；Round 天然把 ±22.5° 内的输入归到最近一档，不需要额外死区。取整 + 按档位查表（而不是直接 Cos/Sin 算值）是为了让"同一个方向"在不同输入下得到**逐位相同**的结果。
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

        /// <summary>回到出生点并满血（打空重来）。</summary>
        /// <remarks>分工是：世界侧（<c>CombatRoot</c>）决定"什么时候重来"，玩家侧执行"回到哪、满血、停住"。出生点理论上在图内，但它是运行期读到的位置；越界时钳回来，否则玩家会回到一张"看不见自己"的地图外。</remarks>
        public void RespawnToSpawn()
        {
            if (Logic == null) return;

            Vector2 position = _spawnPoint;

            if (_bounds.TryClamp(position, out Vector2 clamped)) position = clamped;

            Logic.RespawnTo(position);
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

            _bounds = ReadBounds();
            _world = new WorldInfo(Vector2.zero, in _bounds); // 首帧前也不留 default
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

        /// <summary>装配玩家逻辑。<b>由 <c>GameRoot</c> 在第一个被驱动的帧调</b>（见 <see cref="ISceneRoot.Attach"/>）。</summary>
        /// <remarks><b>读表放在这里而不是 <c>Awake</c></b>：<c>ConfigModule</c> 由 <c>GameRoot.Awake</c> 装配（含第二段 <c>BindAssets</c>），而组件之间的 <c>Awake</c> 顺序 Unity 不保证 —— 写在 <c>Awake</c> 里就是一次"看运气"的启动崩溃（<c>ConfigModule</c> 在未就绪时会抛）。
        /// 输入缓冲也在这里建：它的容量来自配表，装配顺序只有一条 —— 先拿到 <c>PlayerSpec</c>，再建缓冲。</remarks>
        public void Attach()
        {
            if (Logic != null || motor == null) return;

            _spec = ConfigModule.GetPlayer();

            // 缓冲容量取"容量参数"与各输入窗口的较大者：容量小于任何窗口时，窗口内的按下会被挤出历史。
            _buffer = new InputBuffer(
                Mathf.Max(_spec.InputBufferSeconds, _spec.DashBufferSeconds),
                Mathf.RoundToInt(1f / Time.fixedDeltaTime)
                );

            _cameraPlaneDepth = _spec.CameraPlaneDepth;

            Logic = new PlayerLogic(motor, _spec, _buffer);
        }

        /// <summary>物理帧：由 <c>GameRoot.FixedUpdate</c> 按 <see cref="Order"/> 驱动（物理帧只有一个发起者，"玩家先于战斗结算"因此是代码事实而不是 Unity 抽签）。</summary>
        public void FixedTick(float deltaTime)
        {
            // 装配失败（引用未接线）或还没装配时整体 no-op：不读半装配状态
            if (Logic == null) return;

            InputSnapshot raw = inputProvider.ConsumeSnapshot();

            Vector2 move = SnapMoveToEightDirections(raw.Move, _spec.SnapToEightDirections);

            // 归一化后的方向要写回快照：WorldInfo 与 InputBuffer 必须是同一份方向，否则 MoveGroup 取冲刺方向、PlayerLogic 记"最近朝向"时会看到另一份（未处理的）值。
            var snapshot = new InputSnapshot(move, raw.DashPressed, raw.GrabHeld);

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

            // 保险丝：只在真的越界时写位置，避免每帧打断刚体的位置积分。
            if (_world.Bounds.TryClamp(motor.Position, out Vector2 clamped))
            {
                motor.SetPosition(clamped);
            }
        }

        /// <summary>渲染帧：瞄准 ＋ 开火意图（由 <c>GameRoot.Update</c> 按 <see cref="Order"/> 驱动）。</summary>
        /// <remarks><b>瞄准在渲染帧而不在物理帧</b>：帧相位口径是"物理帧只放角色移动与参与物理的逻辑"，而瞄准既不吃物理也不产出物理量。"屏幕 → 世界"只有本类做得了，换算完把<b>世界点</b>交给逻辑层，吸附到哪一格由 <c>TileAim</c> 算 —— 于是"看着能扔到、其实扔不到"的根源（两处各算一次）从结构上消失。
        /// <b>暂停 / 菜单下不瞄准也不开火</b>：判据只有"输入开关"一个真值（<c>InputProvider.IsInputEnabled</c>），并存第二个暂停真值就会不一致（订阅晚了就永远收不到那条事件）。</remarks>
        public void RenderTick(float deltaTime)
        {
            if (Logic == null) return;

            // 遮挡是观感，与物理步无关，所以放渲染帧；放在暂停判断之前：暂停时也要保持档位正确。
            UpdateSortingOrder();

            if (inputProvider == null || !inputProvider.IsInputEnabled)
            {
                Logic.ClearAim();
                return;
            }

            Camera camera = aimCamera != null ? aimCamera : Camera.main;

            if (camera == null || inputProvider == null) return;

            float now = Time.time;

            Logic.UpdateAim(AimWorldPoint(camera), now);

            // 攻击输入：动作表里没有攻击动作，它由 InputProvider 直读指针产出（渲染帧语义）。主攻击 = 水球（吃弹药），副攻击 = 土球（不吃）。
            if (inputProvider.AttackPressedThisFrame) Logic.RequestThrow(BallType.Water, now);
            if (inputProvider.AltAttackPressedThisFrame) Logic.RequestThrow(BallType.Earth, now);
        }

        /// <summary>按 y 刷新本体的渲染档位（Y-Sort）：玩家本体是场景里摆的 <c>SpriteRenderer</c>，"谁挡住谁"必须每帧按 y 重算（场景里填的 <c>sortingOrder</c> 只是初始值）。</summary>
        private void UpdateSortingOrder()
        {
            if (_body == null) _body = GetComponent<SpriteRenderer>();

            if (_body == null) return;

            _body.sortingOrder = RenderOrder.ActorOrder(Position.y);
        }

        /// <summary>屏幕点 → 世界点。<b>不读相机的 z：</b><c>Camera.main.transform.position.z</c> 被 Cinemachine 每帧驱动，依赖它等于让落点跟着相机插件走；正交相机下给一个足够大的常量深度即可（见 <c>ThrowTuning.cameraPlaneDepth</c>）。</summary>
        private Vector2 AimWorldPoint(Camera camera)
        {
            Vector2 screen = inputProvider.AimScreen;

            Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _cameraPlaneDepth));

            return new Vector2(world.x, world.y);
        }

        /// <summary>把场景里的活动区域碰撞体折算成纯数据矩形：<c>BoxCollider2D</c> → <c>min/max</c> 的折算只发生在表现层这一处，每次 <c>Awake</c> 读一次即可（地图尺寸在运行期不会变）。</summary>
        private BoundsArea ReadBounds()
        {
            if (boundsArea == null || !boundsArea.enabled) return default;

            Bounds b = boundsArea.bounds;

            return new BoundsArea(b.min, b.max);
        }
    }
}
