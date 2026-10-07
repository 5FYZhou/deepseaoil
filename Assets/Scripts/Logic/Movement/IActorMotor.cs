using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 执行器的<b>账本</b>：一帧的速度变更累加区与"帧末一次写出"。
    /// </summary>
    /// <remarks>
    /// <b>为什么它在执行器上而不是在逻辑层：</b>审查已定"把 <c>Motor</c> 开放出来、让 Logic 退化为组合件"。
    /// 速度账本本来就是"这一帧要怎么驱动物理体"这件事，它与 <see cref="IMovementMotor"/> 是同一个对象的
    /// 两副面孔；留在逻辑层只会让每个新角色再长一份同样的字段。
    /// <para><b>帧的边界只有一条：</b><see cref="BeginStep"/> 清空累加区、
    /// <see cref="Commit"/> 在帧末写出<b>恰好一次</b>。调用方只有一个 ——
    /// <c>ActorLogic.FixedTick</c>（它是全工程唯一驱动状态机的地方）。
    /// 这条不变量是"每帧只有一个速度写者"的实现形态，不要从别处调这两个方法。</para>
    /// <para><b>提交分两类：</b>瞬变累进 <c>Δ</c>（单位/秒，不乘 Δt）与加速度累进（单位/秒²，帧末乘 Δt）。
    /// 分开是必要的：冲量是"一次性的速度变化"，力是"持续作用"，混在一处会让击退的距离随帧率变化。</para>
    /// </remarks>
    public interface IActorLedger
    {
        /// <summary>本帧工作速度：帧首真值 ＋ 本帧已提交的全部变更。</summary>
        /// <remarks>
        /// 与 <c>IMovementMotor.Velocity</c>（引擎回读口）是<b>两个不同的读数</b>，
        /// 刻意分开显示：两者不一致即"引擎否决了这次提交"（撞墙、被顶住）。
        /// </remarks>
        Vector2 Velocity { get; }

        /// <summary>本帧净提交的速度变化量（瞬变 ＋ 加速度 × Δt）。</summary>
        Vector2 SubmittedDelta { get; }

        /// <summary>帧首读到的真值（上一物理步结束时引擎里的速度）。</summary>
        Vector2 FrameStartVelocity { get; }

        /// <summary>
        /// 本帧的速度乘数（<c>1</c> = 不缩放）。<b>帧首复位</b>：门禁必须每帧重新提交。
        /// </summary>
        /// <remarks>
        /// <b>写口会夹取值</b>：<c>NaN</c> → <c>1</c>（不起作用），负数 → <c>0</c>（定住而不是反向推），
        /// <c>&gt; 1</c> → <c>1</c>（加速是另一件事）。非数一旦进入速度就会让角色带着非数坐标消失，且不报错。
        /// </remarks>
        float SpeedScale { get; set; }

        /// <summary>帧首：读真值、清空累加区、复位速度乘数与本帧速度上限。</summary>
        /// <param name="now">本步的逻辑时刻（秒）。</param>
        /// <param name="deltaTime">本步时长（秒）。<b>时刻与 Δt 都由驱动方给出</b>，不由执行器自己去问
        /// <c>Time</c> —— 同一帧里"状态机算出来的时刻/Δt"与"账本乘的 Δt"必须是同一个数，
        /// 两处各读一次会让行为随帧率漂（而那种漂不报错）。</param>
        void BeginStep(float now, float deltaTime);

        /// <summary>帧末：有提交才写出一次；零提交帧不改引擎速度（第二个写者因此不会被清掉）。</summary>
        void Commit();

        /// <summary>累加一次冲量：一次性的速度变化，<b>不</b>乘 Δt。</summary>
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>累加一次持续力：单位/秒²，每帧提交，本帧贡献 = 该值 × Δt。</summary>
        void AddForce(Vector2 acceleration);

        /// <summary>接管两个分量：本帧工作速度即为给定值，覆盖已提交的全部变更。</summary>
        void SetVelocity(Vector2 velocity);

        /// <summary>把速度硬钳到上限（按<b>当帧速度</b>整体覆盖）；上限 ≤ 0 表示不限制。</summary>
        void ClampSpeed(float maxSpeed);

        /// <summary>只对<b>本帧</b>生效的速度上限：帧末写出时统一钳一次，帧首自动失效。</summary>
        void SetSpeedLimit(float maxSpeed);

        /// <summary>缩放外力累加强度。俯视角角色填 0；需要被击退 / 被水流推 / 被吸附的角色按需打开。</summary>
        void SetExtraForceScale(float scale);

        /// <summary>启动移动锁：在 <paramref name="now"/> 之后的 <paramref name="duration"/> 秒内不响应移动提交。</summary>
        /// <param name="now">当前的逻辑时刻（秒），由驱动方给出。</param>
        /// <param name="duration">锁定时长（秒）。</param>
        void StartMoveLock(float now, float duration);
    }

    /// <summary>
    /// 角色移动执行器的完整契约：<b>写物理体（<see cref="IMovementMotor"/>）＋ 控制律与账本
    /// （<see cref="Foundation.IStateHost"/> ＋ <see cref="IActorLedger"/>）</b>。
    /// </summary>
    /// <remarks>
    /// <b>这是"把 Motor 开放出来"的落点</b>（审查 §9②/§10② 已定）：状态层与移动层注入的是执行器，
    /// 不是 <c>ActorLogic</c> —— 于是 Logic 退化为组合件，状态自己拿执行器按 <c>ctx</c> 算控制律。
    /// <para><b>为什么分成三个接口而不是一个大接口：</b>
    /// <list type="bullet">
    /// <item><see cref="IMovementMotor"/> 是"写物理体"这一件事，逻辑层的边界钳位只要它；</item>
    /// <item><see cref="Foundation.IStateHost"/> 是状态机能看见的入口，骨架住在地基、不认识角色；</item>
    /// <item><see cref="IActorLedger"/> 是"这一帧的速度账本"，只有驱动状态机的那个地方需要。</item>
    /// </list>
    /// 合成的 <c>IActorMotor</c> 只是"三者都齐了"的记号，供注入点使用；分开的部分各自保持最小。</para>
    /// <para><b>控制律的归属：</b><see cref="Foundation.IStateHost.MoveTowards"/> / <c>BrakeTowards</c> /
    /// <c>SnapVelocity</c> 的实现（渐进逼近、零惯性直达、反向衰减）住在执行器上 ——
    /// 于是它们可以在 EditMode 里用一个不碰引擎的探针直接测，而不是必须先造一个 ActorLogic。</para>
    /// </remarks>
    public interface IActorMotor : IMovementMotor, Foundation.IStateHost, IActorLedger
    {
        /// <summary>角色共用运动参数（与 <see cref="Foundation.IStateHost.Config"/> 同一份）。</summary>
        new CharacterConfig Config { get; }

        /// <summary>
        /// 装配期注入角色运动参数（由角色的组合件调一次）。
        /// </summary>
        /// <param name="config">角色配置（玩家给 SO；敌人由 <c>EnemySpec</c> 按表值造一份）。</param>
        /// <remarks>配置是只读参数而不是每帧状态，所以它<b>不</b>参与 <c>BeginStep</c> 的复位。</remarks>
        void Configure(CharacterConfig config);

        /// <summary>移动层的"走"：有惯性按加速度逼近，零惯性当帧直达。</summary>
        /// <param name="direction">目标方向（可未归一化；零向量表示没有期望方向）。</param>
        /// <param name="speed">该方向上的目标速度（<b>会乘上本帧的速度乘数</b>）。</param>
        new void MoveTowards(Vector2 direction, float speed);

        /// <summary>移动层的"停"：有惯性滑停，零惯性当帧停。</summary>
        new void BrakeTowards();

        /// <summary>急停：速度当帧归零（朝向不变）。</summary>
        void StopMove();

        /// <summary>
        /// 俯视角移动：把速度整体接管为 <paramref name="direction"/> × <paramref name="speed"/>（零惯性直达）。
        /// </summary>
        /// <remarks>与 <c>MoveTowards</c> 的分工：那条是"渐进逼近"的控制律，本条是"当帧直达"，
        /// 给俯视角玩家用 —— 松键当帧停，反向当帧换向。</remarks>
        void MoveDirection(Vector2 direction, float speed);

        /// <summary>面向给定方向；零向量表示"不改朝向"（站住时精灵不会自己翻面）。</summary>
        void FaceTowards(Vector2 direction);
    }
}
