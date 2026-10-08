using DeepseaOil.Generated;
using DeepseaOil.Logic.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Input
{
    /// <summary>输入采样器：GameRoot 每渲染帧调一次，采样点唯一</summary>
    public sealed class InputProvider : MonoBehaviour
    {
        private InputSys _input;

        private GameRoot _root;

        private Vector2 _move;

        private bool _grabHeld;

        private bool _dashPressed;

        private bool _inputEnabled = true;

        /// <summary>本渲染帧瞄准屏幕坐标；是"当前位置"不是"按下沿"</summary>
        public Vector2 AimScreen { get; private set; }

        public bool AttackPressedThisFrame { get; private set; }

        public bool AltAttackPressedThisFrame { get; private set; }

        // 输入开关；暂停/菜单时为 false
        public bool IsInputEnabled => _inputEnabled;

        private void Awake()
        {
            _input = new InputSys();
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            _root.RegisterInputProvider(this);
        }

        private void OnDestroy()
        {
            // 用 Start 抓住的引用，不能再写 GameRoot.Instance：退出 Play/切场景时它可能已销毁，getter 会再 new 一个
            if (_root != null) _root.UnregisterInputProvider(this);
        }

        private void OnEnable()
        {
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            _input.Player.Disable();
        }

        public void Sample()
        {
            // 指针输入先采，不受 _inputEnabled 影响；禁用期间要清掉按下沿，否则恢复那帧会把暂停前的按键当按下。
            SamplePointer();

            if (!_inputEnabled) return;

            _move = Vector2.ClampMagnitude(
                _input.Player.Move.ReadValue<Vector2>(),
                1f
            );

            _grabHeld = _input.Player.Grab.IsPressed();

            // 瞬时输入累积，物理帧侧取走
            _dashPressed |= _input.Player.Dash.WasPressedThisFrame();
        }

        // SamplePointer：瞄准位置 + 左右键按下沿。暂不走 InputSys.inputactions —— 动作表里没有这两个动作，改它要重新生成 95KB 的 InputSys.cs
        // 迁移触发条件：需要"键位重绑"或"手柄投掷"时，把 Attack/AltAttack/Aim 加进 Player map 再重新生成
        private void SamplePointer()
        {
            Mouse mouse = Mouse.current;

            // 禁用时按下沿必须清成 false：指针采样不受动作表开关影响，少了它"暂停时点一下鼠标"会当成开火。
            if (mouse == null || !_inputEnabled)
            {
                AttackPressedThisFrame = false;
                AltAttackPressedThisFrame = false;

                return;
            }

            AimScreen = mouse.position.ReadValue();
            AttackPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            AltAttackPressedThisFrame = mouse.rightButton.wasPressedThisFrame;
        }

        /// <summary>取物理帧输入快照；消费后清除按下沿</summary>
        public InputSnapshot ConsumeSnapshot()
        {
            var snapshot = new InputSnapshot(
                _move,
                _dashPressed,
                _grabHeld
            );

            _dashPressed = false;

            return snapshot;
        }

        public void Clear()
        {
            _move = Vector2.zero;
            _grabHeld = false;
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
