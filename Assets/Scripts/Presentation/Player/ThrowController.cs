using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Combat;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Projectile;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Player
{
    /// <summary>
    /// 投掷：读鼠标 → 吸附到格子 → 建球 → 落地交给结算器。<b>它同时是瞄准环的驱动者</b>。
    /// </summary>
    /// <remarks>
    /// <b>它算出来的格子必须就是球真正会落的那一格。</b>所以吸附只用 <c>TileAim.TryGetAimCell</c>
    /// 一次，落点取该格的几何中心 —— 指示器画什么、球就落什么。
    /// <para><b>不是 <c>MonoBehaviour.Update</c> 自驱</b>：由组合根每渲染帧调 <see cref="Tick"/>，
    /// 帧内顺序因此可预测，而暂停时组合根不调它即可。</para>
    /// <para><b>它不写敌人的血</b>：落地只入队到 <see cref="LandingResolver"/>，世界效果由球效果与格子负责。</para>
    /// </remarks>
    public sealed class ThrowController : MonoBehaviour
    {
        /// <summary>飞行中的球（落地即销毁，这里只做清理与每帧推进）。</summary>
        private readonly List<BallDriver> _flying = new List<BallDriver>();

        /// <summary>球种 → 配置行。</summary>
        private readonly Dictionary<BallType, BallSpec> _balls = new Dictionary<BallType, BallSpec>();

        private readonly Cooldown _cooldown = new Cooldown();

        private PlayerController _player;
        private GridView _gridView;
        private InputProvider _inputProvider;
        private Camera _camera;
        private GridLogic _grid;
        private ThrowTuning _tuning;
        private LandingResolver _resolver;
        private TileAimView _aim;
        private PlayerSpec _playerSpec;
        private Transform _ballRoot;

        private bool _paused;

        /// <summary>当前瞄准格是否有效（暂停、无鼠标、几何非法时为 false）。</summary>
        public bool HasAim { get; private set; }

        /// <summary>
        /// 装配。依赖全部由参数给出（<b>没有 inspector 字段</b>）：组合根建出本组件，
        /// 于是"忘了接线"这种失败模式在本类不存在。
        /// </summary>
        public void Initialize(
            PlayerController player,
            GridView gridView,
            InputProvider inputProvider,
            GridLogic grid,
            ThrowTuning tuning,
            LandingResolver resolver,
            TileAimView aim,
            in PlayerSpec playerSpec,
            IReadOnlyList<BallSpec> balls,
            Transform ballRoot,
            Camera camera)
        {
            _player = player;
            _gridView = gridView;
            _inputProvider = inputProvider;
            _grid = grid;
            _tuning = tuning;
            _resolver = resolver;
            _aim = aim;
            _playerSpec = playerSpec;
            _ballRoot = ballRoot;
            _camera = camera != null ? camera : Camera.main;

            _balls.Clear();

            if (balls != null)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    _balls[balls[i].Type] = balls[i];
                }
            }
        }

        private void OnEnable()
        {
            EventBus<GamePaused>.Subscribe(OnPaused);
            EventBus<GameResumed>.Subscribe(OnResumed);
        }

        private void OnDisable()
        {
            EventBus<GamePaused>.Unsubscribe(OnPaused);
            EventBus<GameResumed>.Unsubscribe(OnResumed);
        }

        private void OnPaused(GamePaused evt)
        {
            _paused = true;

            HideAim();
        }

        private void OnResumed(GameResumed evt)
        {
            _paused = false;
        }

        /// <summary>推进一个渲染帧：先推进在飞的球，再处理瞄准与投掷。</summary>
        /// <param name="deltaTime">本帧时长（<c>Time.deltaTime</c>）；暂停时为 0。</param>
        public void Tick(float deltaTime)
        {
            TickFlyingBalls(deltaTime);

            if (_paused)
            {
                HideAim();
                return;
            }

            if (_player == null || _grid == null || !_grid.Geometry.IsValid)
            {
                HideAim();
                return;
            }

            Vector2 origin = PlayerPosition();

            if (!_gridView.IsWired || _inputProvider == null || _camera == null)
            {
                HideAim();
                return;
            }

            // 球种取"水球"的行来定射程：射程属于投掷这一组参数，与球种无关（表里两行同值）。
            if (!_balls.TryGetValue(BallType.Water, out BallSpec water)) return;

            Vector2 mouseWorld = MouseWorldPoint();

            // 几何是属性（每次访问都返回一份值），要按 `in` 传就必须先落到局部变量上。
            GridGeometry geometry = _grid.Geometry;

            if (!TileAim.TryGetAimCell(
                    in geometry,
                    origin,
                    mouseWorld,
                    water.Throw.MaxThrowDistance,
                    out Vector3Int cell))
            {
                HasAim = false;
                HideAim();
                return;
            }

            HasAim = true;

            bool hasAmmo = HasAmmo();

            if (_aim != null) _aim.Show(cell, _grid.Geometry.CellCenter(cell), hasAmmo);

            TryThrow(cell, hasAmmo);
        }

        /// <summary>清掉在飞的球（切场景 / 清场）。</summary>
        public void ClearBalls()
        {
            for (int i = 0; i < _flying.Count; i++)
            {
                if (_flying[i] != null) Destroy(_flying[i].gameObject);
            }

            _flying.Clear();
        }

        private void TryThrow(Vector3Int cell, bool hasAmmo)
        {
            if (_cooldown == null || _inputProvider == null) return;

            float now = Time.time;

            if (!_cooldown.CanUse(now)) return;

            if (_inputProvider.AttackPressedThisFrame)
            {
                // 没弹药时按左键不消耗冷却：否则"空点一下"会白白吃掉半秒。
                if (!hasAmmo) return;

                if (!_player.Logic.Stats.TryConsumeWater(1)) return;

                if (Throw(BallType.Water, cell)) _cooldown.MarkUsed(now, _playerSpec.AttackInterval);

                return;
            }

            if (_inputProvider.AltAttackPressedThisFrame)
            {
                if (Throw(BallType.Earth, cell)) _cooldown.MarkUsed(now, _playerSpec.AttackInterval);
            }
        }

        private bool Throw(BallType type, Vector3Int cell)
        {
            if (!_balls.TryGetValue(type, out BallSpec spec)) return false;

            Vector2 origin = PlayerPosition();
            Vector2 target = _grid.Geometry.CellCenter(cell);

            // 距离与落点取自**同一次**计算：两处各夹一次必然会漂移，
            // 表现是"看着能扔到、其实扔不到"。
            float distance = Vector2.Distance(origin, target);

            var data = new BallData(type, origin, target, distance, in spec.Throw);

            CreateBall(in data, in spec);

            return true;
        }

        /// <summary>建球根物体，并按固定顺序建球本体与阴影。</summary>
        private void CreateBall(in BallData data, in BallSpec spec)
        {
            Color color = CombatPalette.BallColor(data.Type);

            var go = new GameObject($"球_{data.Type}");

            // 保持世界坐标：即使 ballRoot 带着位移，球也不会跟着偏。
            go.transform.SetParent(_ballRoot, true);

            // 根物体是**悬空**的：贴地位置再抬一个出手高度。
            // 球的视觉子物体用相对高度叠加，所以会继承这个抬高量（正是我们要的）；
            // 而阴影自己写世界坐标，因此不会被它带上去。
            float originHeight = _tuning != null ? _tuning.originHeight : 0f;

            go.transform.position = new Vector3(data.Start.x, data.Start.y + originHeight, 0f);

            float ballRadius = _tuning != null ? _tuning.ballRadiusMeters : 0.22f;

            var view = go.AddComponent<BallView>();
            view.Initialize(RenderOrder.Ball, ballRadius * 2f, color);

            // 先 view 再 shadow：AddComponent 会立刻跑子物体的 Awake，顺序写死才不会让两帧的顺序飘。
            var shadowGo = new GameObject("阴影");
            shadowGo.transform.SetParent(go.transform, false);

            var shadow = shadowGo.AddComponent<BallShadow>();
            shadow.Initialize(
                _tuning,
                RenderOrder.BallShadow,
                (_tuning != null ? _tuning.shadowRadiusMeters : 0.20f) * 2f,
                new Color(0f, 0f, 0f, 0.35f));

            var driver = go.AddComponent<BallDriver>();
            driver.Initialize(in data, view, shadow, originHeight, OnBallLanded);

            _flying.Add(driver);
        }

        /// <summary>球落地：只入队，不在这里碰物理（冲量必须在物理帧施加）。</summary>
        private void OnBallLanded(Vector2 point, BallType type)
        {
            if (_resolver == null) return;

            if (!_balls.TryGetValue(type, out BallSpec spec)) return;

            _resolver.Enqueue(point, type, in spec);
        }

        /// <summary>推进在飞的球，并清掉已经销毁的引用。</summary>
        private void TickFlyingBalls(float deltaTime)
        {
            // 倒序：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                BallDriver ball = _flying[i];

                if (ball == null)
                {
                    _flying.RemoveAt(i);
                    continue;
                }

                ball.Tick(deltaTime);
            }
        }

        /// <summary>
        /// 本帧有没有水球可投。弹药归<b>玩家侧账本</b>（<c>PlayerLogic.Stats</c>），
        /// 本类只是读它 —— 收口前它手里还捏着一份 <c>PlayerResources</c> 引用。
        /// </summary>
        private bool HasAmmo()
        {
            return _player != null && _player.Logic != null && _player.Logic.Stats.WaterBallCount > 0;
        }

        private void HideAim()
        {
            HasAim = false;

            if (_aim != null) _aim.Hide();
        }

        private Vector2 PlayerPosition()
        {
            Vector3 p = _player.transform.position;

            return new Vector2(p.x, p.y);
        }

        /// <summary>
        /// 屏幕点 → 世界点。
        /// </summary>
        /// <remarks>
        /// <b>不读相机的 z：</b><c>Camera.main.transform.position.z</c> 被 Cinemachine 每帧驱动，
        /// 依赖它等于让落点跟着相机插件走。正交相机下给一个足够大的常量深度即可
        /// （见 <c>ThrowTuning.cameraPlaneDepth</c>）。
        /// </remarks>
        private Vector2 MouseWorldPoint()
        {
            Vector2 screen = _inputProvider.AimScreen;

            float depth = _tuning != null ? _tuning.cameraPlaneDepth : 100f;

            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

            return new Vector2(world.x, world.y);
        }
    }
}
