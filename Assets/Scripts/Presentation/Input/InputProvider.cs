using DeepseaOil.Generated;
using DeepseaOil.Logic.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Input
{
    /// <summary>输入采样器。由 <c>GameRoot</c> 每渲染帧在顺序表最前面调一次 <see cref="Sample"/>，采样点唯一。</summary>
    /// <remarks>物理帧侧由消费者调 <see cref="ConsumeSnapshot"/> 取走按下沿（取走即清零）。</remarks>
    public sealed class InputProvider : MonoBehaviour
    {
        private InputSys _input;

        /// <summary>注册时抓住的 GameRoot 引用；销毁期只经它退订（理由见 <see cref="OnDestroy"/>）。</summary>
        private GameRoot _root;

        private Vector2 _move;

        private bool _grabHeld;

        private bool _dashPressed;

        private bool _inputEnabled = true;

        /// <summary>本渲染帧的瞄准屏幕坐标（鼠标位置）；瞄准是"当前位置"不是"按下沿"，不参与 <see cref="ConsumeSnapshot"/> 的清零语义。</summary>
        public Vector2 AimScreen { get; private set; }

        public bool AttackPressedThisFrame { get; private set; }

        public bool AltAttackPressedThisFrame { get; private set; }

        // 输入是否被允许（暂停 / 菜单时为 false）；消费者用它回答"这一帧要不要读输入" —— 比各自去订阅暂停事件可靠（暂停事件是一次发布，订阅晚了的组件永远收不到）。
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
            // 用 Start 里抓住的引用，不能在这里再写 GameRoot.Instance：退出 Play / 切场景时它可能已被销毁，那时 getter 会当场再 new 一个 GameRoot 出来。
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
            // 指针类输入先采，且不受 _inputEnabled 影响：禁用期间必须把上一帧的按下沿清掉，否则恢复的那一帧会把暂停前的按键当成"刚按下"。
            SamplePointer();

            if (!_inputEnabled) return;

            _move = Vector2.ClampMagnitude(
                _input.Player.Move.ReadValue<Vector2>(),
                1f
            );

            _grabHeld = _input.Player.Grab.IsPressed();

            // 瞬时输入：累积到物理帧侧被 ConsumeSnapshot 取走
            _dashPressed |= _input.Player.Dash.WasPressedThisFrame();
        }

        // SamplePointer：瞄准位置 + 左右键按下沿。暂不走 InputSys.inputactions —— 动作表里没有攻击与瞄准动作，改它要重新生成 95KB 的 Generated/Input/InputSys.cs。
        // 迁移触发条件：出现"键位重绑"或"手柄投掷"需求时，把 Attack / AltAttack（Button，绑 <Mouse>/leftButton 与 rightButton）与 Aim（Value/Vector2，绑 <Mouse>/position）加进 Player map，重新生成 InputSys 再改本方法。
        private void SamplePointer()
        {
            Mouse mouse = Mouse.current;

            // 输入被禁用时必须把**按下沿**清成 false：指针采样不受动作表开关影响，少了这道闸，"暂停时点一下鼠标"会被当成一次真实开火（瞄准位置照常更新）。
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

        /// <summary>获取当前物理帧的输入快照。消费后清除按下沿。</summary>
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
