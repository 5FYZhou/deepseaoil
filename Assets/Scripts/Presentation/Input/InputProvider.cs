using DeepseaOil.Generated;
using DeepseaOil.Logic.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Input
{
    /// <summary>
    /// 输入采样器。
    /// 使用 Input System 生成的 InputSys 读取输入，
    /// <b>由 <c>GameRoot</c> 每渲染帧调一次 <see cref="Sample"/></b>，
    /// 在物理帧侧由消费者调 <see cref="ConsumeSnapshot"/> 取走按下沿。
    /// </summary>
    /// <remarks>
    /// <b>它同时是战斗切片的"指针采样点"</b>（瞄准位置与左右键），见 <see cref="SamplePointer"/>：
    /// 那一组输入暂时直读 <c>Mouse.current</c>，理由与迁移路径写在那个方法的注释里。
    /// <para><b>收口前它是第二个自驱入口</b>（自己的 <c>Update</c>）：它采样的时刻与
    /// <c>GameRoot.Update</c> 的先后由 Unity 决定，而"按下沿必须在同一帧被采到"是硬需求 ——
    /// 所以改成由 <c>GameRoot</c> 在顺序表最前面调 <see cref="Sample"/>，
    /// 采样点仍然唯一，但顺序第一次成为代码里的事实。</para>
    /// <para><b>它自己向 <c>GameRoot</c> 报到</b>（<c>Start</c> 注册、<c>OnDestroy</c> 注销），
    /// 不再需要谁在 Inspector 里拖它。</para>
    /// </remarks>
    public sealed class InputProvider : MonoBehaviour
    {
        private InputSys _input;

        /// <summary>注册时抓住的 GameRoot 引用；销毁期只经它退订（理由见 <see cref="OnDestroy"/>）。</summary>
        private GameRoot _root;

        private Vector2 _move;

        private bool _grabHeld;

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

        /// <summary>
        /// 输入当前是否被允许（暂停 / 菜单时为 <c>false</c>）。
        /// </summary>
        /// <remarks>消费者用它回答"这一帧要不要读输入" —— 比各自去订阅暂停事件可靠：
        /// 暂停事件是一次发布，订阅晚了的组件永远收不到（而组件之间的生命周期顺序 Unity 不保证）。</remarks>
        public bool IsInputEnabled => _inputEnabled;

        private void Awake()
        {
            _input = new InputSys();
        }

        private void Start()
        {
            // 场景对象自己报到：装配顺序由 Order 决定，不由"谁先在 Inspector 里被拖上"
            _root = GameRoot.Instance;
            _root.RegisterInputProvider(this);
        }

        private void OnDestroy()
        {
            // 用 Start 里抓住的引用，**不能**在这里再写 GameRoot.Instance：
            // 退出 Play / 切场景时 GameRoot 可能已经先被销毁，那时 Instance 的 getter
            // 会当场再 new 一个 GameRoot 出来（并在关闭过程中跑一遍装配）——
            // 这类"销毁期又造对象"的行为表现为一堆收尾日志与泄漏警告。
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

        /// <summary>
        /// 采样一个渲染帧的输入。由 <c>GameRoot.Update</c> 在顺序表最前面调一次。
        /// </summary>
        public void Sample()
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

            // 瞬时输入：累积到物理帧侧被 ConsumeSnapshot 取走
            _dashPressed |= _input.Player.Dash.WasPressedThisFrame();
        }

        /// <summary>
        /// 采样指针：瞄准位置 + 左右键按下沿。
        /// </summary>
        /// <remarks>
        /// <b>为什么暂不走 <c>InputSys.inputactions</c>：</b>那个动作表里<b>没有</b>攻击与瞄准动作
        /// （Player map 只有 Move / Interaction / Pause / Dash / Grab），
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

            // 输入被禁用时必须把**按下沿**清成 false：指针采样不受动作表开关影响，
            // 少了这道闸，"暂停时点一下鼠标"会被当成一次真实开火。
            // 瞄准位置照常更新 —— 它是"当前位置"而不是按下沿，禁用期间留着没有副作用。
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

        /// <summary>
        /// 获取当前物理帧的输入快照。
        /// 消费后清除按下沿。
        /// </summary>
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