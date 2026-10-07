<<<<<<< HEAD
using System;
=======
﻿using DeepseaOil.Logic;
using System.Collections;
>>>>>>> main
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
<<<<<<< HEAD
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;
=======
using DeepseaOil.Logic.Services;
>>>>>>> main

namespace DeepseaOil.Presentation
{
    /// <summary>唯一的真单例：<b>谁造、谁清、别人怎么拿到它</b> —— 进程里只有本类有答案。</summary>
    /// <remarks><b>它是常驻对象</b>（<c>DontDestroyOnLoad</c>）：每个场景重建一次就会重新 <c>new</c> 一份 <c>UIMgr</c> / 音频池 / <c>GameState</c>，而旧的那份还活着。
    /// <b>驱动入口收敛到 1 个（渲染帧）＋ 1 个（物理帧）</b>：若让 <c>PlayerController</c>、<c>InputProvider</c> 各自被 Unity 直接调用，先后由引擎决定 —— 而接触结算依赖"玩家这一帧的速度已经提交"，输入采样依赖"按下沿在同一帧被采到"。
    /// 顺序表（<see cref="Update"/>）：ⓐ 输入采样 → ⓑ UI 输入段 → ① 进程级服务（<c>unscaledDeltaTime</c>）→ ② <c>AssetModule.Tick</c> → ③ 场景根 <c>RenderTick</c>（玩家侧 → 世界侧）→ ④ <c>EffectModule.Tick</c>；③④ 用 <c>Time.deltaTime</c>，暂停时一起冻结。<c>Order</c> 越小越先跑，顺序错了没有任何结构拦得住，所以顺序是数据。
    /// <b>物理帧</b>（<see cref="FixedUpdate"/>）：玩家侧先跑（先提交速度），世界侧后跑（落地冲量 → 敌人 → 受击）。
    /// 三个 static 模块的生命周期也在本类：<c>ConfigModule</c> 先于 <c>AssetModule</c>，<c>EffectModule.Preload</c> 又依赖 <c>AssetModule</c>；拆除顺序必须反过来，且 <c>AssetModule.Dispose</c> 必须最后（别人要经它归还引用计数）。</remarks>
    public sealed class GameRoot : Singleton<GameRoot>
    {
        /// <summary>场景根：按 <see cref="ISceneRoot.Order"/> 升序排列，小者先跑。</summary>
        private readonly List<ISceneRoot> _sceneRoots = new();

        private readonly List<ISceneRoot> _pendingAttach = new();

        /// <summary>进程级服务：<b>加进来的顺序既是 Init 序也是每帧 Tick 序</b>。</summary>
        private readonly List<IService> _services = new();

        private InputProvider _input;

        private UIInputProvider _uiInputProvider;
        private ITickable _uiInput;
<<<<<<< HEAD
        private IGameTime _gameTime;

        /// <summary>真正装配了进程级件的那个实例；只有它负责拆（重复的 <c>GameRoot</c> 被销毁时不许拆掉别人正在用的缓存）。</summary>
        private static GameRoot _assembled;

        /// <summary>渲染帧驱动项：<c>Order</c>（升序执行）＋ <c>Label</c>（诊断用）＋ 一个步骤；<b>顺序是数据而不是方法体里的一段注释</b>。</summary>
        private readonly struct DriveStep
=======

        private List<IService> services = new();

        private TimerManager _timerService;

        /// <summary>真正装配了 Data 层的那个 GameRoot；只有它负责拆（"谁 Init 谁 Dispose"）。</summary>
        private static GameRoot _dataLayerOwner;

        private IGameTime _gameTime;

        /// <summary>
        /// Data 层装配。放在 Awake 而不是 Start：Unity 保证所有 Awake 都先于任何 Start，
        /// 这样别的脚本（例如 ConfigLoader）在自己的 Start 里就能确定性地拿到已就绪的配置。
        /// 顺序不能反：AssetModule 的 Key 来自 ConfigModule。
        /// 失败即抛（ConfigModule.Init 的契约）：带病数据不进运行时。
        ///
        /// 幂等守卫：`GameRoot` 是**场景对象**（没有 `DontDestroyOnLoad`），而 `ConfigModule` 是
        /// 进程级常驻、`Init` 只许调一次（第二次直接抛）。所以"重开本关""从关卡 A 进关卡 B"这类
        /// 二次进入场景，会撞上第一次留下的 `_ready`——必须先问再 Init。
        /// `AssetModule` 侧则相反：它在 `OnDestroy` 里被 `Dispose` 过，`_initialized` 已复位，
        /// 再 `Init` 是合法的；这里的守卫只为防同一帧里出现第二个 `GameRoot`。
        /// </summary>
        private void Awake()
>>>>>>> main
        {
            public readonly int Order;
            public readonly string Label;
            public readonly Action<float> Step;

            public DriveStep(int order, string label, Action<float> step)
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

        /// <summary>装配是否完成（失败时 <see cref="Update"/> 整体 no-op）。</summary>
        public bool IsReady { get; private set; }

        /// <summary>UI：面板的显示 / 隐藏 / 关闭最上层。<b>面板也走这里拿</b>，没有第二个入口。</summary>
        public UIMgr UI { get; private set; }

        /// <summary>游戏状态机（菜单 / 暂停 / 进行）：切状态会连带切面板与暂停意图。</summary>
        public GameManager Game { get; private set; }

        public AudioManager Audio { get; private set; }

        /// <summary>注册一个场景根（重复注册是 no-op）；真正的装配推迟到第一个被驱动的帧。</summary>
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

        /// <summary>注册输入采样器：<b>每帧的采样由本类发起</b>（见 <see cref="Update"/> 的 ⓐ）。</summary>
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

            // 重复的 GameRoot 走到这里时已被判销毁（Destroy 延期到帧末），必须再判一次：否则它会在这一帧里把 Data 层重新装配一遍。
            if (!ReferenceEquals(Instance, this)) return;

            // 必须在 SceneService.Init（在 Assemble 里）之前订阅（EventBus 的发布是"快照 + 按订阅顺序调用"）；不写进 SceneService：它属于 Logic 层，调 EffectModule 会造成反向依赖。
            EventBus<RequestChangeScene>.Subscribe(OnRequestChangeScene);

            Assemble();

            IsReady = true;
        }

        /// <summary>切场景复位清单里的表现层那一步：在 LoadScene 之前清空所有特效实例。</summary>
        private void OnRequestChangeScene(RequestChangeScene evt)
        {
            EffectModule.CleanAll();
        }

        /// <summary>进程级装配：Data 层 → UI / 游戏状态 → 音频 → 服务表 → UI 输入逻辑。</summary>
        /// <remarks>顺序不能反：<c>AssetModule</c> 的 Key 来自 <c>ConfigModule</c>；<c>EffectModule.Preload</c> 走 <c>AssetModule</c> 的同步窄路；<c>UIMgr.Init</c> 与 <c>AudioManager.Init</c> 都要读资源。失败即抛：带病数据不进运行时。</remarks>
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

<<<<<<< HEAD
            _uiInput = new UIInputLogic(UI, Game);
=======
            _gameTime = new GameTime();
>>>>>>> main

            var pauseService = new PauseService(_gameTime);
            var sceneService = new SceneService(pauseService);
            var saveService = new SaveService();

            // 顺序 [Pause, Scene, Save, Audio] 既是 Init 序也是每帧 Tick 序
            pauseService.Init();
            sceneService.Init();
            saveService.Init();
            Audio.Init();

            _services.Add(pauseService);
            _services.Add(sceneService);
            _services.Add(saveService);
            _services.Add(Audio);

<<<<<<< HEAD
            BuildDriveSteps();
=======
            _timerService = TimerManager.Instance;
            _timerService.Init();

            _uiInput = new UIInputLogic(UIMgr.Instance);
>>>>>>> main

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

        /// <summary>把驱动顺序写成数据。<b>加一个阶段是加一行</b>，不是改 <c>Update</c> 的方法体。</summary>
        private void BuildDriveSteps()
        {
            _updateSteps.Clear();
            _fixedSteps.Clear();

            // ⓪ 场景根装配：只在本帧有新注册者时真的做事（幂等，见 EnsureAttached）
            _updateSteps.Add(new DriveStep(0, "场景根装配 EnsureAttached", _ => EnsureAttached()));

            // ⓐ 输入采样：全工程唯一采样点，必须在任何消费者之前（按下沿只在动态更新里有效）
            _updateSteps.Add(new DriveStep(10, "输入采样 InputProvider.Sample", _ => _input?.Sample()));

            _updateSteps.Add(new DriveStep(20, "UI 输入段", _ =>
            {
                _uiInputProvider.Sample();
                _uiInput.Tick(new UILogicContext(_uiInputProvider.ConsumeSnapshot(), Game.CurState));
            }));

            // ① 进程级服务：暂停 / 切场景 / 存档 / 音频。用 unscaledDeltaTime（暂停时也要能推进）
            _updateSteps.Add(new DriveStep(30, "进程级服务 Services.Tick", _ => TickServices(Time.unscaledDeltaTime)));

            _updateSteps.Add(new DriveStep(40, "Data 层 AssetModule.Tick", dt => AssetModule.Tick(dt)));

            // ③ 场景根（渲染帧）：玩家侧（瞄准 / 投掷意图）→ 世界侧（格子 / 球 / 掉落物 / 喷泉）
            _updateSteps.Add(new DriveStep(50, "场景根 RenderTick", dt => TickSceneRootsRender(dt)));

            // ④ 特效：放在场景根之后 —— 本帧新播的特效当帧就被推进一次
            _updateSteps.Add(new DriveStep(60, "特效 EffectModule.Tick", dt => EffectModule.Tick(dt)));

            _fixedSteps.Add(new DriveStep(0, "场景根 FixedTick", dt => TickSceneRootsPhysics(dt)));
        }

        private void RunUpdateSteps()
        {
            RunSteps(_updateSteps, Time.deltaTime);
        }

        private void RunFixedSteps()
        {
            RunSteps(_fixedSteps, Time.fixedDeltaTime);
        }

        /// <summary>按 <c>Order</c> 升序执行一张顺序表；每次调用都排一次序（项数个位数），不依赖"谁先被 <c>Add</c>"。</summary>
        private static void RunSteps(List<DriveStep> steps, float deltaTime)
        {
            steps.Sort(static (a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].Step(deltaTime);
            }
        }

        private void TickServices(float unscaledDeltaTime)
        {
            for (int i = 0; i < _services.Count; i++)
            {
                _services[i].Tick(unscaledDeltaTime);
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

        /// <summary>渲染帧通道：<b>按 <see cref="UpdateSteps"/> 的 <c>Order</c> 升序跑</b>（全工程唯一入口）。</summary>
        private void Update()
        {
<<<<<<< HEAD
            if (!IsReady) return;

            RunUpdateSteps();
=======
            // 输入采样
            _uiInputProvider.Sample();
            // 获取快照
            var snapshot = _uiInputProvider.ConsumeSnapshot();
            _uiInput.Tick(new UILogicContext(snapshot, GameManager.Instance.CurState));

            // 服务
            foreach (var service in services)
            {
                service.Tick(Time.unscaledDeltaTime);
            }

            // Data 层唯一被允许的主动行为：异步队列 / 冷却期 / LRU 淘汰（蓝图 §2 每帧时序 step ②）
            AssetModule.Tick(Time.deltaTime);

            // 特效：顺序表第 ③ 步。用 dt 而不是 unscaledDeltaTime —— 暂停（timeScale = 0）时特效整体冻结
            EffectModule.Tick(Time.deltaTime);

            // 战斗切片：顺序表第 ④ 步。同样用 dt —— 暂停时它拿到的是 0，各子系统自然冻结。
            // 由本类驱动而不是让 CombatRoot 自驱 Update：蓝图契约 #2 "每帧只有四个驱动入口"。
            if (combat != null) combat.Tick(Time.deltaTime);

            _timerService.Tick(Time.deltaTime, Time.unscaledDeltaTime);

            //actors.Tick(Time.deltaTime);

            //views.Tick(Time.unscaledDeltaTime);

            //debugOverlay.Tick();

            //EventBus<FrameEnded>.Publish(new FrameEnded());
>>>>>>> main
        }

        /// <summary>物理帧通道：<b>按 <see cref="FixedSteps"/> 的 <c>Order</c> 升序跑</b>（场景根内部再按各自的 <c>ISceneRoot.Order</c>：玩家侧先跑，世界侧后跑）。</summary>
        /// <remarks>若让两个 <c>MonoBehaviour</c> 各自被 Unity 调，谁先跑由 Unity 决定，而接触结算读的是"玩家这一帧提交后的位置"。暂停时 Unity 不跑本方法，所以不需要额外挡一层。</remarks>
        private void FixedUpdate()
        {
            if (!IsReady) return;

            RunFixedSteps();
        }

        /// <summary>进程退出：<b>按装配的逆序拆</b>。</summary>
        /// <remarks>顺序不能反：<c>EffectModule</c> 与 <c>UIMgr</c> 释放资源要经 <c>AssetModule</c> 归还引用计数，所以 <c>AssetModule.Dispose</c> 必须最后；<c>AudioManager</c> 在服务表里，由服务表统一按反序 <c>Dispose</c>。</remarks>
        private void OnDestroy()
        {
            // 退订放在守卫之前：任何 GameRoot 都要拆掉自己的订阅
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

        /// <summary>把新注册的场景根按 <see cref="ISceneRoot.Order"/> 装配一次（幂等）。</summary>
        /// <remarks>装配推迟到这里而不是各自的 <c>Start</c>：Unity 不保证组件之间的 <c>Awake</c> / <c>Start</c> 顺序，而装配既要读配表、世界侧又要读玩家侧的 <c>Logic</c>。先移出待装列表再调：某个场景根装配失败（抛异常）时不该每帧重试。</remarks>
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
            // 场景根是 MonoBehaviour：对象被销毁后引用还在（接口不参与 Unity 的 null 判定），不剔掉就会在下一帧对已销毁对象调方法。
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
