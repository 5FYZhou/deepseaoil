using DeepseaOil.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.UI;
using DeepseaOil.Data;
using DeepseaOil.Logic.Input;

namespace DeepseaOil.Presentation
{
    public class GameRoot : MonoBehaviour
    {
        [Tooltip("战斗切片组合根（投掷 / 格子 / 敌人 / 血量）。留空则本场景没有战斗内容。")]
        [SerializeField] private CombatRoot combat = default;

        private UIInputProvider _uiInputProvider;
        private ITickable uiInput;
        private List<IService> services = new();

        /// <summary>真正装配了 Data 层的那个 GameRoot；只有它负责拆（"谁 Init 谁 Dispose"）。</summary>
        private static GameRoot _dataLayerOwner;

        private IGameTime gameTime;

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
        {
            // 切场景复位的 Presentation 侧那一步，必须在 SceneService.Init（在 Init() 里）**之前**订阅：
            // EventBus 的发布是"快照 + 按订阅顺序调用"，先订阅者先执行，
            // 于是 CleanAll 稳定地发生在 SceneService.Load（内部会 LoadScene）之前。
            // 为什么不写在 SceneService.Load 里：SceneService 属于 Logic 层，
            // 调 EffectModule（Presentation 层）会造成反向依赖。
            EventBus<RequestChangeScene>.Subscribe(OnRequestChangeScene);

            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            if (!AssetModule.IsInitialized)
            {
                AssetModule.Init();
                _dataLayerOwner = this;
            }

            // 顺序不能反：EffectModule.Preload 依赖 AssetModule（同步窄路）
            if (!EffectModule.IsInitialized)
            {
                EffectModule.Init();
                EffectModule.Preload();
            }

            Init();
        }

        /// <summary>切场景复位清单第 ⑤ 项：在 LoadScene 之前清空所有特效。</summary>
        /// <remarks>
        /// 注：<c>SceneService.Load</c> 内部会 <c>EventBus.ClearAll()</c>，所以本订阅只对**第一次**
        /// 切场景生效；换场景后由新场景的 <c>GameRoot.Awake</c> 重新订阅。
        /// 真正的兜底是 <c>OnDestroy</c> 里的 <c>EffectModule.Dispose()</c>（内部含 CleanAll）。
        /// </remarks>
        private void OnRequestChangeScene(RequestChangeScene evt)
        {
            EffectModule.CleanAll();
        }

        private void Init()
        {
            _uiInputProvider = new UIInputProvider();
            _uiInputProvider.Init();

            gameTime = new GameTime();

            var pauseService = new PauseService(gameTime);
            var sceneService = new SceneService(pauseService);
            var saveService = new SaveService();
            var audioService = AudioManager.Instance;

            pauseService.Init();
            sceneService.Init();
            saveService.Init();
            audioService.Init();

            services.Add(pauseService);
            services.Add(sceneService);
            services.Add(saveService);
            services.Add(audioService);

            uiInput = new UIInputLogic(UIMgr.Instance);

            if (GameManager.Instance.CurState == GameState.None)
            {
                GameManager.Instance.ChangeState(GameState.Menu);
            }
            else Debug.LogWarning("第一次切换游戏状态的不是GameRoot");
        }

        private void Update()
        {
            // ① 输入采样
            _uiInputProvider.Sample();

            // ② 获取快照
            var snapshot = _uiInputProvider.ConsumeSnapshot();
            
            uiInput.Tick(new UILogicContext(snapshot, GameManager.Instance.CurState));

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

            //actors.Tick(Time.deltaTime);

            //views.Tick(Time.unscaledDeltaTime);

            //debugOverlay.Tick();

            //EventBus<FrameEnded>.Publish(new FrameEnded());
        }

        /// <summary>
        /// 物理帧通道：战斗切片的结算入口（落地冲量 / 敌人 / 玩家受击）。
        /// </summary>
        /// <remarks>
        /// <b>为什么不放进 <c>PlayerController.FixedUpdate</c>：</b>那个入口是"玩家"的组合根，
        /// 让它去驱动敌人与落地结算会把玩家与战斗内容绑死。
        /// <c>GameRoot</c> 本来就是驱动点（<c>Update</c> 通道的那个），这里只是补上它的物理帧通道。
        /// <para>暂停时 Unity 不跑 <c>FixedUpdate</c>（<c>timeScale = 0</c>），所以不需要额外挡一层。</para>
        /// </remarks>
        private void FixedUpdate()
        {
            if (combat != null) combat.FixedTick(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 进程退出：清异步队列 / 缓存 / 合并列表（蓝图 §4 启动装配序）。
        /// 只由装配过 Data 层的那个实例来拆——否则叠加场景里第二个 `GameRoot` 被销毁时，
        /// 会把第一个还在用的缓存一起清掉。
        /// </summary>
        private void OnDestroy()
        {
            // 退订放在守卫之前：任何 GameRoot 都要拆掉自己的订阅，
            // 否则残留委托会指向已销毁的对象（并且持有它的引用）。
            EventBus<RequestChangeScene>.Unsubscribe(OnRequestChangeScene);

            if (_dataLayerOwner != this)
                return;

            // 顺序不能反：EffectModule 释放资源要经 AssetModule 归还引用计数
            EffectModule.Dispose();
            AssetModule.Dispose();
            _dataLayerOwner = null;
            _uiInputProvider.Dispose(); 
            foreach (var service in services)
            {
                service.Dispose();
            }
        }
    }
}
