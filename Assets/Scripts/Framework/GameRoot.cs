using DeepseaOil.Logic;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using DeepseaOil.Presentation.UI;
using DeepseaOil.Config;

namespace DeepseaOil.Presentation
{
    public class GameRoot : MonoBehaviour
    {
        private List<IService> services = new();

        private IGameTime gameTime;

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

            //actors.Tick(Time.deltaTime);

            //views.Tick(Time.unscaledDeltaTime);

            //debugOverlay.Tick();

            //EventBus<FrameEnded>.Publish(new FrameEnded());
        }
    }
}
