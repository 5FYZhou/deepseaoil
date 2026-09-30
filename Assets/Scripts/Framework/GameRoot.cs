using DeepseaOil.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.UI;
using DeepseaOil.Config;
using DeepseaOil.Data;

namespace DeepseaOil.Presentation
{
    public class GameRoot : MonoBehaviour
    {
        private List<IService> services = new();

        private IGameTime gameTime;

        /// <summary>
        /// Data 层装配。放在 Awake 而不是 Start：Unity 保证所有 Awake 都先于任何 Start，
        /// 这样别的脚本（例如 ConfigLoader）在自己的 Start 里就能确定性地拿到已就绪的配置。
        /// 顺序不能反：AssetModule 的 Key 来自 ConfigModule。
        /// 失败即抛（ConfigModule.Init 的契约）：带病数据不进运行时。
        /// </summary>
        private void Awake()
        {
            ConfigModule.InitFromStreamingAssets();
            AssetModule.Init();
        }

        private void Start()
        {
            UIMgr.Instance.ShowPanel<BeginPanel>();
            gameTime = new GameTime();

            var pauseService = new PauseService(gameTime);
            var sceneService = new SceneService(gameTime, pauseService);
            var saveService = new SaveService();

            pauseService.Init();
            sceneService.Init();
            saveService.Init();

            services.Add(pauseService);
            services.Add(sceneService);
            services.Add(saveService);

        }

        private void Update()
        {
            //inputProvider.Tick();

            foreach (var service in services)
            {
                service.Tick(Time.unscaledDeltaTime);
            }

            // Data 层唯一被允许的主动行为：异步队列 / 冷却期 / LRU 淘汰（蓝图 §3 图 2 step ③）
            AssetModule.Tick(Time.deltaTime);

            //actors.Tick(Time.deltaTime);

            //views.Tick(Time.unscaledDeltaTime);

            //debugOverlay.Tick();

            //EventBus<FrameEnded>.Publish(new FrameEnded());
        }

        /// <summary>进程退出：清异步队列 / 缓存 / 合并列表（蓝图 §3.2 图 7）。</summary>
        private void OnDestroy()
        {
            AssetModule.Dispose();
        }
    }
}
