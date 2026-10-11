using DeepseaOil.Generated;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using UnityEngine;

namespace DeepseaOil.Presentation.Input
{
    public sealed class UIInputProvider
    {
        private readonly InputSys _input;

        private bool _escPressed;
        private bool _inputEnabled = true;

        public UIInputProvider()
        {
            _input = new InputSys();
            Init();
        }

        public void Init()
        {
            _input.UI.Enable();
            EventBus<GameStateChanged>.Subscribe(OnGameStateChange);
        }

        public void Dispose()
        {
            _input.UI.Disable();
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChange);
        }

        public void Sample()
        {
            if (!_inputEnabled)
                return;

            _escPressed = _input.UI.Cancel.WasPressedThisFrame();
        }

        public UIInputSnapshot ConsumeSnapshot()
        {
            return new UIInputSnapshot(_escPressed);
        }

        public void Clear()
        {
            _escPressed = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;

            if (!enabled)
                Clear();
        }

        private void OnGameStateChange(GameStateChanged evt)
        {
            if(evt.state == Logic.GameState.CG)
            {
                SetInputEnabled(false);
            }
            else
            {
                SetInputEnabled(true);
            }
        }

    }
}