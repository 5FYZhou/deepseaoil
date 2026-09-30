using DeepseaOil.Generated;
using DeepseaOil.Logic.Input;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 输入采样器。
    /// 使用 Input System 生成的 InputSys 读取输入，
    /// 在 Update 中采样，在 FixedUpdate 对应的调用方中消费。
    /// </summary>
    public sealed class InputProvider : MonoBehaviour
    {
        private InputSys _input;

        private Vector2 _move;

        private bool _jumpHeld;
        private bool _grabHeld;

        private bool _jumpPressed;
        private bool _dashPressed;

        private bool _inputEnabled = true;

        private void Awake()
        {
            _input = new InputSys();
        }
        
        private void OnEnable()
        {
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            _input.Player.Disable();
        }

        private void Update()
        {
            if (!_inputEnabled) return;

            // 持续输入
            _move = Vector2.ClampMagnitude(
                _input.Player.Move.ReadValue<Vector2>(),
                1f
            );

            _jumpHeld = _input.Player.Jump.IsPressed();
            _grabHeld = _input.Player.Grab.IsPressed();

            // 瞬时输入：累积到被 FixedUpdate 消费
            _jumpPressed |= _input.Player.Jump.WasPressedThisFrame();
            _dashPressed |= _input.Player.Dash.WasPressedThisFrame();
        }

        /// <summary>
        /// 获取当前物理帧的输入快照。
        /// 消费后清除按下沿。
        /// </summary>
        public InputSnapshot ConsumeSnapshot()
        {
            var snapshot = new InputSnapshot(
                _move,
                _jumpPressed,
                _jumpHeld,
                _dashPressed,
                _grabHeld
            );

            _jumpPressed = false;
            _dashPressed = false;

            return snapshot;
        }

        public void Clear()
        {
            _move = Vector2.zero;
            _jumpHeld = false;
            _grabHeld = false;
            _jumpPressed = false;
            _dashPressed = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;

            if (!enabled)
                Clear();
        }
    }
}