using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.Adapters;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 唯一的真单例：<b>谁造、谁清、别人怎么拿到它</b>——进程里只有本类有答案。
    /// </summary>
    /// <remarks>
    /// <b>它是常驻对象（<c>DontDestroyOnLoad</c>）。</b>这一点是"进程级件由它 <c>new</c> 并持有"的
    /// 必然推论：若它每个场景重建一次，新的 <c>GameRoot</c> 就会重新 <c>new</c> 一份 <c>UIMgr</c> /
    /// 音频池 / <c>GameState</c> —— 缓存、已开面板、当前游戏状态全部重来，而旧的那份还在
    /// <c>DontDestroyOnLoad</c> 场景里活着。所以：<b>场景根改注册制</b>（见 <see cref="ISceneRoot"/>），
    /// <c>GameRoot</c> 自己常驻。场景里放不放 <c>GameRoot</c> 都行：放了的那份若不是第一个，
    /// <c>Singleton&lt;T&gt;.Awake</c> 会把它销毁（它不装配任何东西，见 <see cref="Awake"/> 的守卫）。
    ///
    /// <para><b>驱动入口收敛到 1 个（渲染帧）+ 1 个（物理帧）：</b>收口前
    /// <c>PlayerController.FixedUpdate</c> 与 <c>InputProvider.Update</c> 各自被 Unity 直接调用，
    /// 与 <c>GameRoot.Update</c> 的先后<b>由引擎决定</b>——而战斗切片的接触结算依赖
    /// "玩家这一帧的速度已经提交"，输入采样依赖"按下沿在同一帧被采到"。这不是可以留给引擎的自由，
    /// 所以两者都改成由本类按固定顺序驱动。</para>
    ///
    /// <para><b>顺序表</b>（<see cref="Update"/>）：ⓐ 输入采样 → ⓑ UI 输入段 →
    /// ① 进程级服务（<c>unscaledDeltaTime</c>）→ ② <c>AssetModule.Tick</c> →
    /// ③ 场景根 <c>RenderTick</c>（玩家侧 → 世界侧）→ ④ <c>EffectModule.Tick</c>。
    /// ③④ 用 <c>Time.deltaTime</c>：暂停时 <c>timeScale = 0</c>，两者一起冻结。
    /// <para><b>物理帧</b>（<see cref="FixedUpdate"/>）：按 <c>Order</c> 驱动场景根的
    /// <c>FixedTick</c>——玩家侧先跑（先提交速度、先读输入），世界侧后跑（落地冲量 → 敌人 → 受击）。</para>
    ///
    /// <para><b>三个 static 模块的生命周期也在本类</b>：<c>ConfigModule</c> 先于 <c>AssetModule</c>
    /// （后者的 Key 来自前者），<c>EffectModule.Preload</c> 又依赖 <c>AssetModule</c>；
    /// 拆除顺序必须反过来，且 <c>AssetModule.Dispose</c> 必须最后（别人要经它归还引用计数）。
    /// 它们没有 <c>Reset</c>：表与缓存都是进程级只读件，"复位"没有语义；
    /// 切场景的复位口是 <c>AssetModule.OnSceneSwitch</c> 与 <c>EffectModule.CleanAll</c>。</para>
    /// </remarks>
    public sealed class GameRoot : Singleton<GameRoot>
    {
        /// <summary>场景根：按 <see cref="ISceneRoot.Order"/> 升序排列，小者先跑。</summary>
        private readonly List<ISceneRoot> _sceneRoots = new();

        /// <summary>已注册、还没装配的场景根（每帧最多清一次，见 <see cref="EnsureAttached"/>）。</summary>
        private readonly List<ISceneRoot> _pendingAttach = new();

        /// <summary>进程级服务：<b>加进来的顺序既是 Init 序也是每帧 Tick 序</b>。</summary>
        private readonly List<IService> _services = new();

        /// <summary>输入采样器（场景里的 <c>InputProvider</c> 自己注册进来）。</summary>
        private InputProvider _input;

        private UIInputProvider _uiInputProvider;
        private ITickable _uiInput;
        private IGameTime _gameTime;

        /// <summary>真正装配了进程级件的那个实例；只有它负责拆（"谁 Init 谁 Dispose"）。</summary>
        /// <remarks>
        /// 常驻之后同一进程里只该有一个装配者，这个字段看起来冗余 —— 它守的是
        /// "场景里那份重复的 <c>GameRoot</c> 被销毁时不许把别人正在用的缓存拆掉"。
        /// </remarks>
        private static GameRoot _assembled;

        // ─────────────────────────────────────────────
        // 驱动顺序表：顺序是数据，不是方法体里的一段注释
        // ─────────────────────────────────────────────

        /// <summary>
        /// 渲染帧驱动项：<c>Order</c>（升序执行）＋ <c>Label</c>（诊断用）＋ 一个步骤。
        /// </summary>
        /// <remarks>
        /// <b>为什么把顺序变成数据：</b>收口前它是 <c>Update</c> 方法体里的一段注释
        /// （ⓐ → ⓑ → ①②③④），加一个阶段就要改方法体，而顺序错了没有任何结构拦得住。
        /// 变成表之后，"谁在谁之前"是这张表可读的一列。
        /// <para><b>代价写在这里：</b>步骤签名是 <c>Action&lt;float&gt;</c>，
        /// 所以 <c>Time.deltaTime</c> / <c>unscaledDeltaTime</c> 的差别只能靠传入值表达 ——
        /// 每个步骤自己决定传什么（见下面的 lambda）。</para>
        /// </remarks>
        private readonly struct DriveStep
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

        /// <summary>渲染帧顺序表（按 <c>Order</c> 升序执行）。</summary>
        public IReadOnlyList<(int Order, string Label)> UpdateSteps => Describe(_updateSteps);

        /// <summary>物理帧顺序表（按 <c>Order</c> 升序执行）。</summary>
        public IReadOnlyList<(int Order, string Label)> FixedSteps => Describe(_fixedSteps);

        /// <summary>装配是否完成（失败时 <see cref="Update"/> 整体 no-op）。</summary>
        public bool IsReady { get; private set; }

        /// <summary>UI：面板的显示 / 隐藏 / 关闭最上层。<b>面板也走这里拿</b>，不再有第二个入口。</summary>
        public UIMgr UI { get; private set; }

        /// <summary>游戏状态机（菜单 / 暂停 / 进行）：切状态会连带切面板与暂停意图。</summary>
        public GameManager Game { get; private set; }

        /// <summary>音频：音效 / 音乐 / 音量。<c>Dispose</c> 由服务表负责（见 <see cref="OnDestroy"/>）。</summary>
        public AudioManager Audio { get; private set; }

        // ─────────────────────────────────────────────
        // 注册口（场景对象自己报到）
        // ─────────────────────────────────────────────

        /// <summary>注册一个场景根（重复注册是 no-op）。</summary>
        /// <remarks>注册只记名；真正的装配推迟到第一个被驱动的帧（见 <see cref="EnsureAttached"/>）。</remarks>
        public void RegisterSceneRoot(ISceneRoot root)
        {
            if (root == null || _sceneRoots.Contains(root)) return;

            _sceneRoots.Add(root);
            _sceneRoots.Sort(CompareSceneRoots);

            if (!_pendingAttach.Contains(root)) _pendingAttach.Add(root);
        }

        /// <summary>注销一个场景根（场景对象销毁时调；没注册过是 no-op）。</summary>
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

        /// <summary>注销输入采样器。</summary>
        public void UnregisterInputProvider(InputProvider provider)
        {
            if (ReferenceEquals(_input, provider)) _input = null;
        }

        protected override void Awake()
        {
            // Singleton<T>.Awake：不是第一个就把自己销毁，并让 instance 指向第一个。
            base.Awake();

            // 重复的 GameRoot 走到这里时已经被判了销毁（Destroy 延期到帧末），
            // 所以必须**再判一次**：否则它会在这一帧里把 Data 层重新装配一遍。
            if (!ReferenceEquals(Instance, this)) return;

            // 切场景复位的表现层那一步，必须在 SceneService.Init（在 Assemble 里）**之前**订阅：
            // EventBus 的发布是"快照 + 按订阅顺序调用"，先订阅者先执行，
            // 于是 CleanAll 稳定地发生在 SceneService.Load（内部会 LoadScene）之前。
            // 为什么不写在 SceneService.Load 里：SceneService 属于 Logic 层，
            // 调 EffectModule（Presentation 层）会造成反向依赖。
            EventBus<RequestChangeScene>.Subscribe(OnRequestChangeScene);

            Assemble();

            IsReady = true;
        }

        /// <summary>切场景复位清单里的表现层那一步：在 LoadScene 之前清空所有特效实例。</summary>
        /// <remarks>
        /// 注：<c>SceneService.Load</c> 内部会 <c>EventBus.ClearAll()</c>，所以本订阅只对**第一次**
        /// 切场景生效；换场景后由"常驻的 GameRoot"重新订阅（本类是常驻对象，订阅在 <c>Awake</c> 里
        /// 只做一次 —— 这也是把 <c>GameRoot</c> 改成常驻的附带收益）。
        /// </remarks>
        private void OnRequestChangeScene(RequestChangeScene evt)
        {
            EffectModule.CleanAll();
        }

        /// <summary>
        /// 进程级装配：Data 层 → UI / 游戏状态 → 音频 → 服务表 → UI 输入逻辑。
        /// </summary>
        /// <remarks>
        /// 顺序不能反：<c>AssetModule</c> 的 Key 来自 <c>ConfigModule</c>；
        /// <c>EffectModule.Preload</c> 走 <c>AssetModule</c> 的同步窄路；<c>UIMgr.Init</c> 与
        /// <c>AudioManager.Init</c> 都要读资源。
        /// <para>失败即抛（<c>ConfigModule.Init</c> 的契约）：带病数据不进运行时。</para>
        /// </remarks>
        private void Assemble()
        {
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            if (!AssetModule.IsInitialized)
            {
                AssetModule.Init();
                _assembled = this;
            }

            // ConfigModule 的第二段：表已就绪（上一段）、AssetModule 也已就绪 —— SO 侧的取值
            // （PlayerConfig / ThrowTuning / DropTuning）在这里才绑得上。顺序不能反：
            // ConfigModule.Init → AssetModule.Init → ConfigModule.BindAssets（见 ConfigModule 的类注释）。
            ConfigModule.BindAssets();

            if (!EffectModule.IsInitialized)
            {
                EffectModule.Init();
                EffectModule.Preload();
            }

            _gameTime = new GameTime();

            // UI：三件套在 Init 里建（构造不做 IO），失败即抛 —— 没有 UI 的游戏状态机没有意义
            UI = new UIMgr();
            UI.Init();

            Game = new GameManager(UI);

            // 音频宿主是 GameRoot 自己：它是常驻对象，音频根挂在它下面即可跨场景存活。
            // 收口前这里有两个入口（AudioRoot 自建 + AudioManager 找不到就 new 一个），现在只剩一个。
            Audio = new AudioManager(transform);

            _uiInputProvider = new UIInputProvider();
            _uiInputProvider.Init();

            // UI 输入逻辑拿到两个窄口：面板操作（IUIOperation）＋ 状态请求（IUIStateRequest）。
            // 它自己不认识 GameManager —— 逻辑层调表现层只能经接口。
            _uiInput = new UIInputLogic(UI, Game);

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

            BuildDriveSteps();

            if (Game.CurState == GameState.None)
            {
                Game.ChangeState(GameState.Menu);
            }
            else
            {
                // 走到这里说明别的入口先切过一次状态。本类不再覆盖它（那是别人的意图），
                // 但必须说清后果：本局不会再有任何东西替它回到 Menu / Running。
                Debug.LogWarning(
                    "[GameRoot] 第一次切换游戏状态的不是 GameRoot（CurState 已非 None）。" +
                    "本类不再改它 —— 若那个入口不是有意的，游戏会停在一个没人维护的状态里。");
            }
        }

        // ─────────────────────────────────────────────
        // 顺序表的装配与执行
        // ─────────────────────────────────────────────

        /// <summary>
        /// 把驱动顺序写成数据。<b>加一个阶段是加一行</b>，不是改 <c>Update</c> 的方法体。
        /// </summary>
        /// <remarks>
        /// 编号沿用类注释里的约定：<c>Order</c> 越小越先跑。ⓐ/ⓑ 与 ①-④ 的差别只在编号的"来源"
        /// （前者是"面板操作"，不吃 <c>dt</c>），执行上它们是一条线。
        /// </remarks>
        private void BuildDriveSteps()
        {
            _updateSteps.Clear();
            _fixedSteps.Clear();

            // ⓪ 场景根装配：只在本帧有新注册者时真的做事（幂等，见 EnsureAttached）
            _updateSteps.Add(new DriveStep(0, "场景根装配 EnsureAttached", _ => EnsureAttached()));

            // ⓐ 输入采样：全工程唯一采样点，必须在任何消费者之前（按下沿只在动态更新里有效）
            _updateSteps.Add(new DriveStep(10, "输入采样 InputProvider.Sample", _ => _input?.Sample()));

            // ⓑ UI 输入段：先采样快照再消费，本帧状态取自 UILogicContext（不再有第二个来源）
            _updateSteps.Add(new DriveStep(20, "UI 输入段", _ =>
            {
                _uiInputProvider.Sample();
                _uiInput.Tick(new UILogicContext(_uiInputProvider.ConsumeSnapshot(), Game.CurState));
            }));

            // ① 进程级服务：暂停 / 切场景 / 存档 / 音频。用 unscaledDeltaTime（暂停时也要能推进）
            _updateSteps.Add(new DriveStep(30, "进程级服务 Services.Tick", _ => TickServices(Time.unscaledDeltaTime)));

            // ② Data 层唯一被允许的主动行为：异步队列 / 冷却期 / LRU 淘汰
            _updateSteps.Add(new DriveStep(40, "Data 层 AssetModule.Tick", dt => AssetModule.Tick(dt)));

            // ③ 场景根（渲染帧）：玩家侧（瞄准 / 投掷意图）→ 世界侧（格子 / 球 / 掉落物 / 喷泉）
            _updateSteps.Add(new DriveStep(50, "场景根 RenderTick", dt => TickSceneRootsRender(dt)));

            // ④ 特效：放在场景根之后 —— 本帧新播的特效当帧就被推进一次
            _updateSteps.Add(new DriveStep(60, "特效 EffectModule.Tick", dt => EffectModule.Tick(dt)));

            // 物理帧只有一个阶段，编号沿用类注释
            _fixedSteps.Add(new DriveStep(0, "场景根 FixedTick", dt => TickSceneRootsPhysics(dt)));
        }

        /// <summary>按 <c>Order</c> 升序跑渲染帧顺序表。</summary>
        private void RunUpdateSteps()
        {
            RunSteps(_updateSteps, Time.deltaTime);
        }

        /// <summary>按 <c>Order</c> 升序跑物理帧顺序表。</summary>
        private void RunFixedSteps()
        {
            RunSteps(_fixedSteps, Time.fixedDeltaTime);
        }

        /// <summary>
        /// 按 <c>Order</c> 升序执行一张顺序表。
        /// </summary>
        /// <remarks>每次调用都排一次序（项数个位数、<c>List.Sort</c> 对近乎有序的输入很便宜）：
        /// 顺序是数据，就不该再依赖"谁先被 <c>Add</c>"。</remarks>
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

        /// <summary>
        /// 渲染帧通道：<b>按 <see cref="UpdateSteps"/> 的 <c>Order</c> 升序跑</b>。
        /// </summary>
        private void Update()
        {
            if (!IsReady) return;

            RunUpdateSteps();
        }

        /// <summary>
        /// 物理帧通道：<b>按 <see cref="FixedSteps"/> 的 <c>Order</c> 升序跑</b>
        /// （场景根内部再按各自的 <c>ISceneRoot.Order</c>：玩家侧先跑，世界侧后跑）。
        /// </summary>
        /// <remarks>
        /// <b>顺序在这里才第一次成为"代码里的事实"</b>：收口前
        /// <c>PlayerController.FixedUpdate</c> 与 <c>GameRoot.FixedUpdate</c> 是两个
        /// <c>MonoBehaviour</c>，谁先跑由 Unity 决定，而 <c>CombatRoot.FixedTick</c> 的接触结算
        /// 读的是"玩家这一帧提交后的位置"。暂停时 Unity 不跑本方法，所以不需要额外挡一层。
        /// </remarks>
        private void FixedUpdate()
        {
            if (!IsReady) return;

            RunFixedSteps();
        }

        /// <summary>
        /// 进程退出：<b>按装配的逆序拆</b>。
        /// </summary>
        /// <remarks>
        /// 顺序不能反：<c>EffectModule</c> 与 <c>UIMgr</c> 释放资源要经 <c>AssetModule</c> 归还引用计数，
        /// 所以 <c>AssetModule.Dispose</c> 必须最后。<c>AudioManager</c> 在服务表里，
        /// 由服务表统一 <c>Dispose</c>。
        /// <para><b>服务表也按反序拆</b>：收口前这里是正序 —— 与类注释"按装配的逆序拆"不一致，
        /// 而当时能跑只是因为各服务的 <c>Dispose</c> 互不依赖（那是巧合，不是设计）。
        /// 服务的加入顺序同时是 Init 序与 Tick 序，拆除取它的逆序，三者因此一致。</para>
        /// </remarks>
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

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        /// <summary>
        /// 把新注册的场景根按 <see cref="ISceneRoot.Order"/> 装配一次（幂等）。
        /// </summary>
        /// <remarks>
        /// <b>为什么装配推迟到这里而不是各自的 <c>Start</c>：</b>见 <see cref="ISceneRoot.Attach"/> ——
        /// 组件之间的 <c>Awake</c> / <c>Start</c> 顺序 Unity 都不保证，而装配既要读配表
        /// （要等 <c>GameRoot.Awake</c>），世界侧又要读玩家侧的 <c>Logic</c>。
        /// 交给本方法按 <c>Order</c> 逐个调，两个问题一起消失。
        /// <para>先移出待装列表再调：某个场景根装配失败（抛异常）时不该每帧重试 ——
        /// 它自己的 <c>IsReady</c> 会让后续 Tick 保持 no-op。</para>
        /// </remarks>
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
            // 场景根是 MonoBehaviour：对象被销毁后引用还在（接口不参与 Unity 的 null 判定），
            // 不剔掉就会在下一帧对已销毁对象调方法。
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
