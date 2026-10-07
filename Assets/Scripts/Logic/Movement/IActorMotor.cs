using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>速度账本：一帧的速度变更累加区 ＋ 帧末一次写出。</summary>
    /// <remarks>帧的边界只有一条：<see cref="BeginStep"/> 清空累加区、<see cref="Commit"/> 在帧末写出恰好一次；
    /// 调用方只有 <c>ActorLogic.FixedTick</c>（全工程唯一驱动状态机的地方），不要从别处调这两个方法。
    /// 提交分两类：瞬变累进（单位/秒，不乘 Δt）与加速度累进（单位/秒²，帧末乘 Δt）；混在一处会让击退距离随帧率变化。</remarks>
    public interface IActorLedger
    {
        /// <summary>本帧工作速度：帧首真值 ＋ 本帧已提交的全部变更。</summary>
        Vector2 Velocity { get; }

        Vector2 SubmittedDelta { get; }

        /// <summary>帧首读到的真值（上一物理步结束时引擎里的速度）。</summary>
        Vector2 FrameStartVelocity { get; }

        /// <summary>本帧的速度乘数（<c>1</c> = 不缩放）。<b>帧首复位</b>：门禁必须每帧重新提交。</summary>
        /// <remarks><b>写口夹取</b>：<c>NaN</c> → <c>1</c>、负数 → <c>0</c>、<c>&gt; 1</c> → <c>1</c>。非数进入速度会让角色带着非数坐标消失，且不报错。</remarks>
        float SpeedScale { get; set; }

        /// <summary>帧首：读真值、清空累加区、复位速度乘数与本帧速度上限。</summary>
        /// <remarks><b>时刻与 Δt 都由驱动方给出</b>（秒），不由执行器自己去问 <c>Time</c>：两处各读一次会让行为随帧率漂，而那种漂不报错。</remarks>
        void BeginStep(float now, float deltaTime);

        /// <summary>帧末：有提交才写出一次；零提交帧不改引擎速度（第二个写者因此不会被清掉）。</summary>
        void Commit();

        /// <summary>累加一次冲量：一次性的速度变化，<b>不</b>乘 Δt。</summary>
        void AddImpulse(Vector2 deltaVelocity);

        /// <summary>累加一次持续力：单位/秒²，每帧提交，本帧贡献 = 该值 × Δt。</summary>
        void AddForce(Vector2 acceleration);

        void SetVelocity(Vector2 velocity);

        /// <summary>把速度硬钳到上限（按<b>当帧速度</b>整体覆盖）；上限 ≤ 0 表示不限制。</summary>
        void ClampSpeed(float maxSpeed);

        /// <summary>缩放外力累加强度。俯视角角色填 0；需要被击退 / 被水流推 / 被吸附的角色按需打开。</summary>
        void SetExtraForceScale(float scale);
    }

    /// <summary>角色移动执行器的完整契约：写物理体（<see cref="IMovementMotor"/>）＋ 控制律与账本（<see cref="Foundation.IStateHost"/> ＋ <see cref="IActorLedger"/>）。<b>只接受配置、不暴露配置</b>：<see cref="Configure"/> 是写口，读口是 <see cref="Foundation.IStateHost.Motion"/> 的六个标量。</summary>
    public interface IActorMotor : IMovementMotor, Foundation.IStateHost, IActorLedger
    {
        /// <summary>角色共用运动参数（<b>只写不读</b>：状态机读的是 <see cref="Foundation.IStateHost.Motion"/>）。</summary>
        CharacterConfig Config { get; }

        /// <summary>装配期注入角色运动参数（由角色的组合件调一次；玩家给 SO，敌人由 <c>EnemySpec</c> 按表值造一份）。</summary>
        void Configure(CharacterConfig config);

        /// <summary>移动层的"走"：有惯性按加速度逼近，零惯性当帧直达。方向可未归一化（零向量 = 没有期望方向），速度会乘上本帧的速度乘数。</summary>
        new void MoveTowards(Vector2 direction, float speed);

        new void BrakeTowards();

        /// <summary>急停：速度当帧归零（朝向不变）。</summary>
        void StopMove();

        /// <summary>俯视角移动：把速度整体接管为 <paramref name="direction"/> × <paramref name="speed"/>（零惯性直达）。</summary>
        /// <remarks>与 <c>MoveTowards</c> 的分工：那条是"渐进逼近"，本条是"当帧直达" —— 松键当帧停，反向当帧换向。</remarks>
        void MoveDirection(Vector2 direction, float speed);

        /// <summary>面向给定方向；零向量表示"不改朝向"（站住时精灵不会自己翻面）。</summary>
        void FaceTowards(Vector2 direction);
    }
}
