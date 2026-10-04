using DeepseaOil.Data;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的逻辑层：持有速度账本，每帧把"追击目标"翻译成一次转向提交。
    /// </summary>
    /// <remarks>
    /// <b>它是敌人速度的唯一写者。</b>击退不是另外去写 <c>Rigidbody2D</c>，而是
    /// <see cref="ApplyKnockback"/> 往账本里加一次冲量 —— 于是"每帧只有一个速度写者"这条
    /// 不变量在敌人身上也成立（玩家那侧由 <c>PlayerLogic</c> 保证）。
    /// <para><b>为什么继承 <c>ActorLogic</c>：</b>基类的账本协议（帧首读引擎真值 → 帧内只累加提交
    /// → 帧末一次写出）就是为"有惯性的角色"设计的。另写一套等于在同一工程里放两个速度真值。</para>
    /// <para><b>不读输入。</b>基类的上下文由 <see cref="Tick"/> 用一份零输入快照组装，
    /// 因为敌人只需要时间与世界边界，而这两者与输入无关。</para>
    /// </remarks>
    public sealed class EnemyLogic : ActorLogic
    {
        /// <summary>
        /// "已经停下来了"的速度阈值（单位/秒）。
        /// </summary>
        /// <remarks>
        /// 取 0.5 而不是 0：速度是指数衰减的，永远不会<b>恰好</b>等于 0，严格判零等于这条恢复逻辑
        /// 永远不生效。也不能取太大（比如 2.0）—— 那样刚被击退、速度还有一大半的时候就判为"停了"，
        /// 滑行距离会明显变短。
        /// </remarks>
        private const float ResumeChaseSpeed = 0.5f;

        private readonly EnemySpec _enemy;

        /// <summary>追击目标（玩家位置）。没有目标时不提交任何移动。</summary>
        private Vector2? _target;

        /// <summary>本帧的减速系数（由组合根每帧喂入）。</summary>
        private float _slowMultiplier = 1f;

        /// <summary>禁足剩余<b>帧数</b>。整帧计数，见 <see cref="IsStunned"/>。</summary>
        private int _stunFramesRemaining;

        /// <summary>
        /// 已经施加、但还没被写进速度账本的击退冲量。
        /// </summary>
        /// <remarks>
        /// <b>为什么冲量必须"挂着"而不是当场写进账本：</b><c>ActorLogic.FixedTick</c> 在<b>帧首</b>
        /// 就把 <c>_delta</c> 清零，而那一刻在 <c>OnTick</c> 之前。于是"在两次 Tick 之间调 <c>AddImpulse</c>"
        /// 会被下一次固定帧的开头<b>静默抹掉</b> —— 冲量从头到尾没到过执行器，而账本看起来一切正常。
        /// <para>这不是测试才有的问题：结算（<c>GridLogic.Deal</c>）与 <see cref="Tick"/>（<c>WaveDirector</c>）
        /// 是<b>两个组件</b>，执行顺序 Unity 不保证。先跑 <c>Tick</c> 的那一帧里冲量就会消失，
        /// 现象是"打中了但敌人纹丝不动"，且没有任何报错。</para>
        /// </remarks>
        private Vector2 _pendingKnockback;

        /// <summary>
        /// 当前是否处于受击后的禁足。
        /// </summary>
        /// <remarks>
        /// <b>整帧计数，不是秒数倒计时。</b>"还剩多久"是离散量（玩家数的是帧），
        /// 而秒数倒计时把它表示成连续量、每帧减一个 Δt：余数落在 0 与一个 Δt 之间时，
        /// <c>&gt; 0f</c> 与 <c>&gt; Δt</c> 会给出相差一帧的答案，两个方向都不对。整数计数没有这个自由度。
        /// <para><b>帧数还取决于"谁来递减、什么时候递减"—— 这一点真踩过：</b>
        /// 曾经在帧首递减，于是 <c>BeginStun(12)</c> 实际只禁足 <b>11</b> 帧，
        /// 第 12 帧已回到追击分支、冲量当场被转向覆盖（实测速度从 5.5 掉到 3.6）。
        /// 那份代码与浮点无关（<c>0.24f / 0.02f</c> 在单精度下正好是 12），纯粹是顺序问题。
        /// 现在递减在帧末，见 <see cref="Tick"/>。</para>
        /// </remarks>
        public bool IsStunned => _stunFramesRemaining > 0;

        /// <summary>本种类敌人数值（只读，供组合根取半径 / 禁足时长等）。</summary>
        public EnemySpec Spec => _enemy;

        /// <param name="motor">移动执行器。参数类型是<b>接口</b>而不是具体 MonoBehaviour：
        /// 逻辑层不该知道"实现挂在物体上"，而测试要能塞一个不碰引擎的探针进来。</param>
        /// <param name="spec">敌人的表值（追击 / 耐久 / 禁足）。</param>
        /// <param name="characterConfig">折算后的角色运动参数（由 <c>EnemyCharacterFactory</c> 给出）。</param>
        public EnemyLogic(IMovementMotor motor, in EnemySpec spec, CharacterConfig characterConfig)
            : base(motor, characterConfig)
        {
            _enemy = spec;
        }

        /// <summary>设置追击目标；传 <c>null</c> 表示"没有目标"（敌人随即滑停）。</summary>
        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        /// <summary>
        /// 设置本帧的减速系数（每帧都要喂；判断该喂多少是组合根的事）。
        /// </summary>
        /// <remarks>
        /// 入口处做一次净化：<c>NaN</c> 参与比较恒为 <c>false</c>，<c>Mathf.Clamp</c> 遇到它会原样返回，
        /// 于是非数会一路传染进速度、角色带着非数坐标消失。非数按"不起作用"（1）处理。
        /// </remarks>
        public void SetSlowMultiplier(float multiplier)
        {
            _slowMultiplier = float.IsNaN(multiplier) ? 1f : Mathf.Clamp01(multiplier);
        }

        /// <summary>
        /// 施加一次击退冲量（速度的瞬时变化）。<b>可以调用在帧外</b> —— 它只挂起，不动账本。
        /// </summary>
        /// <remarks>
        /// <b>与"直接 <c>AddImpulse</c>"的区别是本方法的全部意义。</b>同一帧内多次调用会累加
        /// （被两处同时结算就是两股冲量），这与白模行为一致。
        /// </remarks>
        public void ApplyKnockback(float impulse, Vector2 direction)
        {
            if (impulse <= 0f) return;

            _pendingKnockback += direction * impulse;
        }

        /// <summary>
        /// 开始一次禁足：从被结算那一帧起，接下来按 <paramref name="stepSeconds"/> 折算出的整帧数都处于禁足。
        /// </summary>
        /// <param name="seconds">标称时长（秒）。</param>
        /// <param name="stepSeconds">物理步长（秒），由驱动方给出（<c>Time.fixedDeltaTime</c>）。</param>
        /// <remarks>
        /// <b>取整方向是"向上"，这是刻意的：</b>宁可多禁足半帧也不要少 —— 少一帧会让击退位移缩水，
        /// 而位移是这条机制存在的全部理由。步长非法（<c>≤ 0</c> 或非数）时退回"至少一帧"，不抛异常。
        /// <para>递减在帧末（见 <see cref="Tick"/>），所以本方法设的 N 在紧接着的那一帧里
        /// <b>完整地</b>被读到，"禁足 N 帧"字面成立。</para>
        /// </remarks>
        public void BeginStun(float seconds, float stepSeconds)
        {
            if (seconds <= 0f) return;

            if (float.IsNaN(stepSeconds) || stepSeconds <= 0f) stepSeconds = 0.02f;

            _stunFramesRemaining = Mathf.Max(1, Mathf.CeilToInt(seconds / stepSeconds));
        }

        /// <summary>
        /// 推进一个物理帧。
        /// </summary>
        /// <param name="now">驱动方的时间（<c>Time.fixedTime</c>）。</param>
        /// <param name="deltaTime">驱动方的步长（<c>Time.fixedDeltaTime</c>）。</param>
        /// <remarks>
        /// 用零输入快照组装上下文：本类不吃输入，但时间来源必须是驱动方那一份。
        /// <c>worldInfo</c> 给默认值即语义正确 —— 敌人有刚体，阻挡由物理解算，逻辑层不钳它。
        /// </remarks>
        public void Tick(float now, float deltaTime)
        {
            var context = new LogicContext(now, deltaTime, default, InputSnapshot.Empty);

            FixedTick(context);

            // 禁足的递减在**帧末**，不在帧首（理由见 IsStunned 的注释）。
            // 减完不再判：本帧该不该禁足由 OnTick 里那次 IsStunned 决定，那时读到的还是本帧的值。
            if (_stunFramesRemaining > 0) _stunFramesRemaining--;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // ---- 第 1 步：把挂起的击退冲量落进本帧账本 ----
            //
            // **必须在最前面**，而且**必须在这里**（不能在 ApplyKnockback 里）：
            // FixedTick 在本方法之前的帧首已经把 _delta 清了，只有这一处写才是"本帧的"。
            Vector2 knockback = _pendingKnockback;
            _pendingKnockback = Vector2.zero;

            // 逐分量精确比较，不用 `knockback != Vector2.zero`：
            // `Vector2.operator!=` 带 `kEpsilon² = 1e-10` 的平方容差（模长小于 1e-5 就当零），
            // 实测 3e-6 的冲量会被静默吞掉。当前量级离那个门槛十几个数量级，但这是隐患。
            if (knockback.x != 0f || knockback.y != 0f) AddImpulse(knockback);

            // ---- 第 2 步：禁足 ----
            //
            // 两段合起来才是一次完整的禁足：
            //   ① 冲量那一帧**照常往下走正常提交路径**，于是它真的被写进引擎；
            //      目标速度取"本帧工作速度"，衰减率传 0 ⇒ 不做指数衰减 ⇒ 冲量原样保留。
            //   ② 之后每一帧 `desired == current` ⇒ `delta` 分量相减**恰好为 0** ⇒ 零提交
            //      ⇒ 基类不写速度 ⇒ 引擎保持匀速滑出去。
            //
            // **② 与"速度是不是零"无关**：成立条件是 `desired == current`，所以敌人**正在滑行时**
            // 那一帧同样是零提交、不会去重写速度 —— 否则撞墙或被别的敌人顶住时，
            // 这些重写会**覆盖物理结果**。
            //
            // **曾经写成 `if (IsStunned) return;`（纯提前返回），那是错的，而且错得很隐蔽**：
            // 冲量那一帧也是"零提交"，于是它永远不会被写进引擎 —— 现象是"击退完全没生效"。
            if (IsStunned)
            {
                SteerTowards(Velocity, Velocity.magnitude, 0f, 0f);
                return;
            }

            if (_target == null)
            {
                // 没有目标：把速度滑停（零方向让 SteerTowards 走指数衰减那一支）。
                SteerTowards(Vector2.zero, 0f, _enemy.Acceleration, _enemy.KnockbackDecay);
                return;
            }

            Vector2 target = _target.Value;

            Steering steering = Steering.Resolve(
                Motor.Position,
                target,
                _enemy.StopDistance,
                _enemy.ChaseRange,
                _enemy.MaxSpeed,
                _slowMultiplier);

            // 停止距离之内、但玩家仍在追击范围内：**只在"快停下来了"的时候才重新压上去**。
            //
            // 这一条挡的是一个很难查的自锁：击退会把敌人推到停止距离之外一点点，
            // 而"进入停止距离就不再追"是个纯几何判据 —— 敌人一旦被推进那个区间，
            // 它自己就再也没有理由往回走了。表现是"被撞一次之后敌人黏在原地，
            // 玩家再也挨不到第二下"，不报错、也不像 bug，只是整局只掉一次血。
            //
            // 加上"速度已经很小"这个条件之后：被推着走（速度大）→ 继续滑出去；滑到快停 → 重新压上来。
            // 用 Velocity 而不是引擎回读：那是本类账本里的当帧工作速度，判据要在同一帧内自洽
            // （引擎值滞后一个物理步）。
            if (steering.Speed <= 0f
                && Velocity.sqrMagnitude <= ResumeChaseSpeed * ResumeChaseSpeed
                && Vector2.Distance(Motor.Position, target) > _enemy.StopDistance)
            {
                Steering resume = Steering.Resolve(
                    Motor.Position,
                    target,
                    _enemy.StopDistance,
                    _enemy.ChaseRange,
                    _enemy.MaxSpeed,
                    _slowMultiplier);

                if (!resume.IsIdle) steering = resume;
            }

            SteerTowards(
                steering.Direction,
                steering.Speed,
                _enemy.Acceleration,
                _enemy.KnockbackDecay);
        }
    }
}
