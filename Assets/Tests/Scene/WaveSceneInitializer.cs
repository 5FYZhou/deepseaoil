using DeepseaOil.Presentation;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Tests.Scene
{
    public class WaveSceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            GameRoot root = GameRoot.Instance;
            root.Game.ChangeState(Logic.GameState.Running);
            root.UI.ShowPanel<GamePlayPanel>();
            root.UI.ShowPanel<HudPanel>();
        }
    }
}