using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Service;
using DeepseaOil.Logic.Services;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>唯一真单例：谁造、谁清、别人怎么拿到它</summary>
    /// <remarks>常驻对象（DontDestroyOnLoad），每场景重建会多出一份 UIMgr/音频池/GameState。驱动入口收敛到 1 个渲染帧 + 1 个物理帧（顺序是数据，见 UpdateSteps）。三个 static 模块的生命周期也在这里：ConfigModule 先于 AssetModule，EffectModule.Preload 又依赖 AssetModule；拆除顺序必须反过来，AssetModule.Dispose 必须最后。</remarks>
    public sealed class GameRoot : Singleton<GameRoot>
    {
        private readonly List<ISceneRoot> _sceneRoots = new();

        private readonly List<ISceneRoot> _pendingAttach = new();

        private readonly List<IService> _services = new();

        private InputProvider _input;

        private UIInputProvider _uiInputProvider;
        private ITickable _uiInput;
        private IGameTime _gameTime;

        /// <summary>真正装配了进程级件的那个实例，只有它负责拆</summary>
        private static GameRoot _assembled;

        /// <summary>渲染帧驱动项：Order（升序）+ Label + 一个步骤</summary>
        /// <remarks>步骤收到两个时间：scaledDeltaTime（暂停时为 0）与 unscaledDeltaTime（暂停时照走）；服务一律不自己读 Time，口径由驱动方给。</remarks>
        private readonly struct DriveStep
        {
            public readonly int Order;
            public readonly string Label;
            public readonly Action<float, float> Step;

            public DriveStep(int order, string label, Action<float, float> step)
            {
                Order = order;
                Label = label;
                Step = step;
            }
        }

        private readonly List<DriveStep> _updateSteps = new();
        private readonly List<DriveStep> _fixedSteps = new();

        public IReadOnlyList<(int Order, string Label)> UpdateSteps => Describe(_updateSteps);

        public IReadOnlyList<(int Order, string Label)> FixedSteps => Describe(_fixedSteps);

        public bool IsReady { get; private set; }

        /// <summary>UI：面板显隐与关闭最上层</summary>
        public UIMgr UI { get; private set; }

        public GameManager Game { get; private set; }

        public AudioManager Audio { get; private set; }

        /// <summary>全局计时器（时间轮）：麻痹/冷却/倒计时这类"持续到某时刻"的排这里，不排给已有钟的格子</summary>
        public TimerManager Timer { get; private set; }

        /// <summary>注册场景根（重复是 no-op），真正装配推迟到第一个被驱动的帧</summary>
        public void RegisterSceneRoot(ISceneRoot root)
        {
            if (root == null || _sceneRoots.Contains(root)) return;

            _sceneRoots.Add(root);
            _sceneRoots.Sort(CompareSceneRoots);

            if (!_pendingAttach.Contains(root)) _pendingAttach.Add(root);
        }

        public void UnregisterSceneRoot(ISceneRoot root)
        {
            if (root == null) return;

            _sceneRoots.Remove(root);
            _pendingAttach.Remove(root);
        }

        /// <summary>注册输入采样器；每帧采样由本类发起</summary>
        public void RegisterInputProvider(InputProvider provider)
        {
            if (provider == null) return;

            _input = provider;
        }

        public void UnregisterInputProvider(InputProvider provider)
        {
            if (ReferenceEquals(_input, provider)) _input = null;
        }

        protected override void Awake()
        {
            base.Awake();

            // Destroy 延期到帧末，这里必须再判一次，否则它会重装一遍 Data 层
            if (!ReferenceEquals(Instance, this)) return;

            // 必须在 SceneService.Init 之前订阅（发布按订阅顺序调用）；不写进 SceneService：它属 Logic 层，调 EffectModule 是反向依赖
            EventBus<RequestChangeScene>.Subscribe(OnRequestChangeScene);

            Assemble();

            IsReady = true;
        }

        private void OnRequestChangeScene(RequestChangeScene evt)
        {
            EffectModule.CleanAll();
        }

        /// <summary>进程级装配：Data 层 → UI/状态 → 音频 → 服务表 → UI 输入</summary>
        /// <remarks>顺序不能反：AssetModule 的 Key 来自 ConfigModule，EffectModule.Preload 走 AssetModule；UIMgr.Init 与 AudioManager.Init 都要读资源。失败即抛。</remarks>
        private void Assemble()
        {
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            if (!AssetModule.IsInitialized)
            {
                AssetModule.Init();
                _assembled = this;
            }

            ConfigModule.BindAssets();

            if (!EffectModule.IsInitialized)
            {
                EffectModule.Init();
                EffectModule.Preload();
            }

            _gameTime = new GameTime();

            UI = new UIMgr();
            UI.Init();

            Game = new GameManager(UI);

            Audio = new AudioManager(transform);

            _uiInputProvider = new UIInputProvider();
            _uiInputProvider.Init();

            _uiInput = new UIInputLogic(UI, Game);

            var pauseService = new PauseService(_gameTime);
            var sceneService = new SceneService(pauseService);
            var saveService = new SaveService();

            Timer = new TimerManager(0.1f, 512);

            // 顺序 [Pause, Scene, Save, Audio, Timer] 既是 Init 序也是 Tick 序
            pauseService.Init();
            sceneService.Init();
            saveService.Init();
            Audio.Init();
            Timer.Init();

            _services.Add(pauseService);
            _services.Add(sceneService);
            _services.Add(saveService);
            _services.Add(Audio);
            _services.Add(Timer);

            BuildDriveSteps();

            if (Game.CurState == GameState.None)
            {
                Game.ChangeState(GameState.Menu);
            }
            else
            {
                Debug.LogWarning(
                    "[GameRoot] 第一次切换游戏状态的不是 GameRoot（CurState 已非 None）。" +
                    "本类不再改它 —— 若那个入口不是有意的，游戏会停在一个没人维护的状态里。");
            }
        }

        /// <summary>把驱动顺序写成数据：加阶段是加一行，不改 Update 方法体</summary>
        private void BuildDriveSteps()
        {
            _updateSteps.Clear();
            _fixedSteps.Clear();

            // ⓪ 场景根装配：有新注册者时才做事（幂等）
            _updateSteps.Add(new DriveStep(0, "场景根装配 EnsureAttached", (_, __) => EnsureAttached()));

            // ⓐ 输入采样：全工程唯一采样点，必须在消费者之前
            _updateSteps.Add(new DriveStep(10, "输入采样 InputProvider.Sample", (_, __) => _input?.Sample()));

            _updateSteps.Add(new DriveStep(20, "UI 输入段", (_, __) =>
            {
                _uiInputProvider.Sample();
                _uiInput.Tick(new UILogicContext(_uiInputProvider.ConsumeSnapshot(), Game.CurState));
            }));

            // ① 进程级服务：暂停/切场景/存档/音频/计时器，两个 delta 一起给
            _updateSteps.Add(new DriveStep(30, "进程级服务 Services.Tick", (dt, unscaledDt) => TickServices(dt, unscaledDt)));

            _updateSteps.Add(new DriveStep(40, "Data 层 AssetModule.Tick", (dt, __) => AssetModule.Tick(dt)));

            // ③ 场景根：玩家侧 → 世界侧
            _updateSteps.Add(new DriveStep(50, "场景根 RenderTick", (dt, __) => TickSceneRootsRender(dt)));

            // ④ 特效：放在场景根之后，本帧新播的当帧就推进一次
            _updateSteps.Add(new DriveStep(60, "特效 EffectModule.Tick", (dt, __) => EffectModule.Tick(dt)));

            _fixedSteps.Add(new DriveStep(0, "场景根 FixedTick", (dt, __) => TickSceneRootsPhysics(dt)));
        }

        private void RunUpdateSteps()
        {
            RunSteps(_updateSteps, Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void RunFixedSteps()
        {
            RunSteps(_fixedSteps, Time.fixedDeltaTime, Time.fixedDeltaTime);
        }

        private static void RunSteps(List<DriveStep> steps, float deltaTime, float unscaledDeltaTime)
        {
            steps.Sort(static (a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].Step(deltaTime, unscaledDeltaTime);
            }
        }

        /// <summary>推进服务表；两个 delta 原样转发</summary>
        private void TickServices(float deltaTime, float unscaledDeltaTime)
        {
            for (int i = 0; i < _services.Count; i++)
            {
                _services[i].Tick(deltaTime, unscaledDeltaTime);
            }
        }

        private void TickSceneRootsRender(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IRenderTicked ticked) ticked.RenderTick(deltaTime);
            }
        }

        private void TickSceneRootsPhysics(float deltaTime)
        {
            for (int i = 0; i < _sceneRoots.Count; i++)
            {
                if (_sceneRoots[i] is IPhysicsTicked ticked) ticked.FixedTick(deltaTime);
            }
        }

        private static IReadOnlyList<(int Order, string Label)> Describe(List<DriveStep> steps)
        {
            steps.Sort(static (a, b) => a.Order.CompareTo(b.Order));

            var result = new List<(int Order, string Label)>(steps.Count);

            for (int i = 0; i < steps.Count; i++)
            {
                result.Add((steps[i].Order, steps[i].Label));
            }

            return result;
        }

        private void Update()
        {
            if (!IsReady) return;

            RunUpdateSteps();
        }

        /// <summary>物理帧通道：按 FixedSteps 的 Order 升序跑，场景根内部再按各自 Order</summary>
        /// <remarks>若让两个 MonoBehaviour 各自被 Unity 调，先后由 Unity 决定，而接触结算读的是玩家这一帧提交后的位置。</remarks>
        private void FixedUpdate()
        {
            if (!IsReady) return;

            RunFixedSteps();
        }

        /// <summary>进程退出：按装配逆序拆</summary>
        /// <remarks>EffectModule 与 UIMgr 释放资源要经 AssetModule 归还引用计数，故 AssetModule.Dispose 必须最后；AudioManager 与 TimerManager 由服务表按反序 Dispose。</remarks>
        private void OnDestroy()
        {
            // 退订必须在守卫之前
            EventBus<RequestChangeScene>.Unsubscribe(OnRequestChangeScene);

            if (!ReferenceEquals(_assembled, this)) return;

            IsReady = false;

            EffectModule.Dispose();
            UI?.Dispose();

            for (int i = _services.Count - 1; i >= 0; i--)
            {
                _services[i].Dispose();
            }

            _services.Clear();
            _sceneRoots.Clear();

            AssetModule.Dispose();

            _assembled = null;
        }

        /// <summary>把新注册的场景根按 Order 装配一次（幂等）</summary>
        /// <remarks>装配推迟到这里而非各自 Start：Unity 不保证 Awake/Start 顺序。先移出待装列表再调：某个装配失败时不该每帧重试。</remarks>
        private void EnsureAttached()
        {
            PruneSceneRoots();

            if (_pendingAttach.Count == 0) return;

            _pendingAttach.Sort(CompareSceneRoots);

            ISceneRoot[] attaching = _pendingAttach.ToArray();

            _pendingAttach.Clear();

            for (int i = 0; i < attaching.Length; i++)
            {
                if (!IsAlive(attaching[i])) continue;

                attaching[i].Attach();
            }
        }

        private void PruneSceneRoots()
        {
            // 对象销毁后引用还在（接口不参与 Unity 的 null 判定），不剔掉就会对已销毁对象调方法
            for (int i = _sceneRoots.Count - 1; i >= 0; i--)
            {
                if (!IsAlive(_sceneRoots[i])) _sceneRoots.RemoveAt(i);
            }
        }

        private static bool IsAlive(ISceneRoot root)
        {
            if (root == null) return false;

            return root is UnityEngine.Object o ? o != null : true;
        }

        private static int CompareSceneRoots(ISceneRoot a, ISceneRoot b)
        {
            int oa = IsAlive(a) ? a.Order : int.MaxValue;
            int ob = IsAlive(b) ? b.Order : int.MaxValue;

            return oa.CompareTo(ob);
        }
    }
}
