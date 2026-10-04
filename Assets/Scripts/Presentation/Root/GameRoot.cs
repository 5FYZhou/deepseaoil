using DeepseaOil.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.UI;
using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Services;

namespace DeepseaOil.Presentation
{
    public class GameRoot : MonoBehaviour
    {
        private UIInputProvider _uiInputProvider;
        private ITickable _uiInput;

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
        {
            if (!ConfigModule.IsReady)
                ConfigModule.InitFromStreamingAssets();

            if (!AssetModule.IsInitialized)
            {
                AssetModule.Init();
                _dataLayerOwner = this;
            }

            Init();
        }

        private void Init()
        {
            _uiInputProvider = new UIInputProvider();
            _uiInputProvider.Init();

            _gameTime = new GameTime();

            var pauseService = new PauseService(_gameTime);
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

            _timerService = TimerManager.Instance;
            _timerService.Init();

            _uiInput = new UIInputLogic(UIMgr.Instance);

            if (GameManager.Instance.CurState == GameState.None)
            {
                GameManager.Instance.ChangeState(GameState.Menu);
            }
            else Debug.LogWarning("第一次切换游戏状态的不是GameRoot");
        }

        private void Update()
        {
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

            // Data 层唯一被允许的主动行为：异步队列 / 冷却期 / LRU 淘汰（蓝图 §4 每帧时序 step ②）
            AssetModule.Tick(Time.deltaTime);

            _timerService.Tick(Time.deltaTime, Time.unscaledDeltaTime);

            //actors.Tick(Time.deltaTime);

            //views.Tick(Time.unscaledDeltaTime);

            //debugOverlay.Tick();

            //EventBus<FrameEnded>.Publish(new FrameEnded());
        }

        /// <summary>
        /// 进程退出：清异步队列 / 缓存 / 合并列表（蓝图 §6 启动装配序）。
        /// 只由装配过 Data 层的那个实例来拆——否则叠加场景里第二个 `GameRoot` 被销毁时，
        /// 会把第一个还在用的缓存一起清掉。
        /// </summary>
        private void OnDestroy()
        {
            if (_dataLayerOwner != this)
                return;

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
