using DeepseaOil.Generated;
using DeepseaOil.Logic.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 输入采样器。
    /// 使用 Input System 生成的 InputSys 读取输入，
    /// 在 Update 中采样，在 FixedUpdate 对应的调用方中消费。
    /// </summary>
    /// <remarks>
    /// <b>它同时是战斗切片的"指针采样点"</b>（瞄准位置与左右键），见 <see cref="SamplePointer"/>：
    /// 那一组输入暂时直读 <c>Mouse.current</c>，理由与迁移路径写在那个方法的注释里。
    /// </remarks>
    public sealed class InputProvider : MonoBehaviour
    {
        private InputSys _input;

        private Vector2 _move;

        private bool _grabHeld;

        private bool _jumpPressed;
        private bool _dashPressed;

        private bool _inputEnabled = true;

        /// <summary>
        /// 本渲染帧的瞄准屏幕坐标（鼠标位置）。
        /// </summary>
        /// <remarks>每渲染帧重新采样，<b>不</b>参与 <see cref="ConsumeSnapshot"/> 的清零语义 ——
        /// 瞄准是"当前位置"而不是"按下沿"，物理帧侧不需要它。</remarks>
        public Vector2 AimScreen { get; private set; }

        /// <summary>本渲染帧是否按下主攻击（鼠标左键）。</summary>
        public bool AttackPressedThisFrame { get; private set; }

        /// <summary>本渲染帧是否按下副攻击（鼠标右键）。</summary>
        public bool AltAttackPressedThisFrame { get; private set; }

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
            // 指针类输入**先采**，且不受 _inputEnabled 影响：
            // 禁用期间必须把上一帧的按下沿清掉，否则恢复的那一帧会把暂停前的按键当成"刚按下"。
            SamplePointer();

            if (!_inputEnabled) return;

            // 持续输入
            _move = Vector2.ClampMagnitude(
                _input.Player.Move.ReadValue<Vector2>(),
                1f
            );

            _grabHeld = _input.Player.Grab.IsPressed();

            // 瞬时输入：累积到被 FixedUpdate 消费
            _jumpPressed |= _input.Player.Jump.WasPressedThisFrame();
            _dashPressed |= _input.Player.Dash.WasPressedThisFrame();
        }

        /// <summary>
        /// 采样指针：瞄准位置 + 左右键按下沿。
        /// </summary>
        /// <remarks>
        /// <b>为什么暂不走 <c>InputSys.inputactions</c>：</b>那个动作表里<b>没有</b>攻击与瞄准动作
        /// （Player map 只有 Move / Interaction / Pause / Jump / Dash / Grab），
        /// 而动作表是三层共用的资产，改它要重新生成 95KB 的 <c>Generated/Input/InputSys.cs</c> ——
        /// 生成物的 diff 会把真实改动淹没，且会牵动 UI 侧的 EventSystem 接线。
        /// <para>于是战斗输入暂时由<b>唯一的采样点</b>（本类）直读设备：契约"InputProvider 是唯一采样点"
        /// 仍然成立，缺的是"可重绑 / 手柄支持"。</para>
        /// <para><b>迁移触发条件：</b>当出现"键位重绑"或"手柄投掷"需求时，把
        /// <c>Attack</c> / <c>AltAttack</c>（Button，绑 <c>&lt;Mouse&gt;/leftButton</c> / <c>rightButton</c>）
        /// 与 <c>Aim</c>（Value/Vector2，绑 <c>&lt;Mouse&gt;/position</c>）三条动作加进 Player map，
        /// 重新生成 <c>InputSys</c>，再把本方法的三行换成
        /// <c>_input.Player.Attack.WasPressedThisFrame()</c> 之类的读法即可 —— 其余代码不用动。</para>
        /// <para><c>wasPressedThisFrame</c> 只在动态更新里有效，而本方法在 <c>Update</c> 里被调，
        /// 所以语义成立（与 Jump / Dash 同一条）。</para>
        /// </remarks>
        private void SamplePointer()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                AttackPressedThisFrame = false;
                AltAttackPressedThisFrame = false;

                return;
            }

            AimScreen = mouse.position.ReadValue();
            AttackPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            AltAttackPressedThisFrame = mouse.rightButton.wasPressedThisFrame;
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