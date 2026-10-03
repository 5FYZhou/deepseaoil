using DeepseaOil.Logic;
using DeepseaOil.Logic.Input;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 白模敌人的逻辑层：持有速度账本，每帧把"追击目标"翻译成一次转向提交。
    /// </summary>
    /// <remarks>
    /// <b>它是敌人速度的唯一写者。</b>击退不是另外去写 <c>Rigidbody2D</c>，而是
    /// <see cref="ApplyKnockback"/> 往账本里加一次冲量 —— 于是"每帧只有一个速度写者"这条
    /// 不变量在敌人身上也成立（玩家那侧由 <c>PlayerLogic</c> 保证）。
    /// <para><b>为什么继承 <c>ActorLogic</c> 而不是自己写一套：</b>基类的账本协议
    /// （帧首读引擎真值 → 帧内只累加提交 → 帧末一次写出）就是为"有惯性的角色"设计的，
    /// <c>CharacterConfig</c> 里那三个字段的注释也写着"敌人追击与击退滑行要用"。
    /// 另写一套等于在同一工程里放两个速度真值。</para>
    /// <para><b>不读输入。</b>基类的 <c>Ctx</c> 由本类的 <see cref="Tick"/> 用一份零输入快照组装，
    /// 因为 <c>Ctx</c> 只服务时间（<c>deltaTime</c>）与世界边界，而这两者与输入无关。</para>
    /// </remarks>
    public sealed class EnemyLogic : ActorLogic
    {
        private readonly EnemyConfig _enemy;

        /// <summary>追击目标（玩家位置）。没有目标时不提交任何移动。</summary>
        private Vector2? _target;

        /// <summary>本帧的泥浆减速系数（由组合根每帧喂入）。</summary>
        private float _slowMultiplier = 1f;

        /// <summary>禁足剩余<b>帧数</b>。整帧计数，见 <see cref="IsStunned"/>。</summary>
        private int _stunFramesRemaining;

        /// <summary>
        /// 已经施加、但还没被写进速度账本的击退冲量。
        /// </summary>
        /// <remarks>
        /// <b>为什么冲量必须"挂着"而不是当场写进账本：</b>
        /// <c>ActorLogic.FixedTick</c> 在<b>帧首</b>就把 <c>_delta</c> 清零，而那一刻在 <c>OnTick</c> 之前。
        /// 于是"在两次 Tick 之间调 <c>AddImpulse</c>"会被下一次固定帧的开头<b>静默抹掉</b> ——
        /// 冲量从头到尾没到过执行器，而账本看起来一切正常。
        /// <para>这不是测试才有的问题：<c>TakeDamage</c>（来自 <c>ThrowSpawner.FixedUpdate</c>）与
        /// <see cref="Tick"/>（来自 <c>EnemyDirector.FixedUpdate</c>）是<b>两个组件</b>，
        /// 执行顺序 Unity 不保证。先跑 <c>Tick</c> 的那一帧里冲量就会消失，
        /// 现象是"球砸中了但敌人纹丝不动"，且没有任何报错。</para>
        /// <para>所以冲量先存在这里，由 <see cref="OnTick"/> 在<b>帧内</b>取出来累加 ——
        /// 那时 <c>_delta</c> 已经是"本帧"的了，谁也清不掉它。</para>
        /// </remarks>
        private Vector2 _pendingKnockback;

        /// <summary>
        /// 当前是否处于被砸中后的禁足。
        /// </summary>
        /// <remarks>
        /// <b>整帧计数，不是秒数倒计时。</b>
        /// <para><b>为什么不能用秒数：</b>秒数倒计时靠"每帧减 Δt"累积，而"禁足还剩多久"是个离散量 ——
        /// 玩家数的是帧，代码判的却是浮点余数。余数恰好落在 0 与一个 Δt 之间时，
        /// <c>&gt; 0f</c> 与 <c>&gt; Δt</c> 这两个判据会给出<b>相差一帧</b>的答案，
        /// 而两个方向都错：少一帧会让冲量在变成位移之前就被转向覆盖，
        /// 多一帧会让击退距离比标称值大一帧的量。整数计数根本没有这个自由度。</para>
        /// <para><b>帧数是由"谁来递减、什么时候递减"决定的，不是由残差决定的 —— 这一点踩过。</b>
        /// 曾经把递减放在帧首（<c>OnTick</c> 之前），于是 <c>OnTick</c> 读到的是已经被减掉一个的余量：
        /// <c>BeginStun(12)</c> 实际只禁足 <b>11</b> 帧，第 12 帧已经回到追击分支、
        /// 冲量当场被转向覆盖（实测第 11 帧速度从 5.5 掉到 3.6）。这与浮点无关 ——
        /// <c>0.24f / 0.02f</c> 在单精度下正好是 <c>12</c>，一点残差都没有。
        /// 现在递减在帧末，见 <see cref="Tick"/>。</para>
        /// <para>对标称时长的取舍：<see cref="ThrowConstants.ENEMY_STUN_FRAMES"/> 是**唯一真值**，
        /// 秒数（0.24 秒）只是它按固定步长换算出来的说法。白模的物理步长是定值，
        /// 所以"帧数"比"秒数"更接近玩家实际感受到的东西。</para>
        /// </remarks>
        public bool IsStunned => _stunFramesRemaining > 0;

        /// <param name="motor">移动执行器。参数类型是<b>接口</b>而不是 <c>EnemyMotor</c>：
        /// 逻辑层不该知道"实现是 MonoBehaviour"，而测试要能塞一个不碰引擎的探针进来
        /// （照 <c>移动Tests.LimitProbe</c> 的做法）。</param>
        /// <param name="config">敌人数值。</param>
        public EnemyLogic(IMovementMotor motor, EnemyConfig config) : base(motor, config.BuildCharacterConfig())
        {
            _enemy = config;
        }

        /// <summary>设置追击目标；传 <c>null</c> 表示"没有目标"（敌人随即滑停）。</summary>
        public void SetTarget(Vector2? target)
        {
            _target = target;
        }

        /// <summary>设置本帧的泥浆减速系数（每帧都要喂；判断该喂多少是组合根的事）。</summary>
        public void SetSlowMultiplier(float multiplier)
        {
            _slowMultiplier = multiplier;
        }

        /// <summary>
        /// 施加一次击退冲量（速率的瞬时变化）。<b>可以调用在帧外</b> —— 它只挂起，不动账本。
        /// </summary>
        /// <remarks>
        /// <b>与"直接 <c>AddImpulse</c>"的区别是本方法的全部意义。</b>
        /// 直接累加会在下一次固定帧的帧首被清零丢掉（见 <see cref="_pendingKnockback"/>），
        /// 而挂起之后由 <see cref="OnTick"/> 在帧内取出，就一定进得去。
        /// <para>同一帧内多次调用会累加（被两颗球同时砸中就是两股冲量），这与之前的行为一致。</para>
        /// </remarks>
        public void ApplyKnockback(float impulse, Vector2 direction)
        {
            if (impulse <= 0f) return;

            _pendingKnockback += direction * impulse;
        }

        /// <summary>
        /// 开始一次禁足：<b>从被砸中那一帧起，接下来 <paramref name="seconds"/> 秒按
        /// <paramref name="stepSeconds"/> 折算出的整帧数都处于禁足</b>。
        /// </summary>
        /// <param name="seconds">标称时长（秒）。</param>
        /// <param name="stepSeconds">物理步长（秒），由驱动方给出（<c>Time.fixedDeltaTime</c>）。</param>
        /// <remarks>
        /// <b>取整方向是"向上"，这是刻意的：</b>宁可多禁足半帧也不要少 —— 少一帧会让击退位移缩水，
        /// 而位移是这条机制存在的全部理由。步长非法（<c>≤ 0</c> 或非数）时退回"至少一帧"，
        /// 不抛异常：白模不该因为一个数字崩掉。
        /// <para><b>"从被砸中那一帧起"是这句契约的重点，也是踩过的地方。</b>
        /// 本方法与 <see cref="Tick"/> 由<b>两个组件</b>驱动（<c>EnemyActor.TakeDamage</c> 与
        /// <c>EnemyDirector.FixedUpdate</c>），谁先谁后 Unity 不保证，所以帧数不能依赖调用顺序。
        /// 递减放在帧末（见 <see cref="Tick"/>）之后：本方法设的 N 在紧接着的那一帧里
        /// <b>完整地</b>被 <see cref="OnTick"/> 读到，于是"禁足 N 帧"字面成立。
        /// 曾经递减在帧首，实际只禁足 N-1 帧。</para>
        /// <para>白模的实际取值是 <c>0.24 / 0.02 = 12</c>，正好整数 —— 因为
        /// <c>ENEMY_STUN_DURATION</c> 与 <c>ENEMY_STUN_FRAMES</c> 已经对齐成同一个数。
        /// 见 <c>ThrowConstants.ENEMY_STUN_FRAMES</c> 的注释。</para>
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
        /// <remarks>
        /// <paramref name="now"/> 与 <paramref name="deltaTime"/> 是<b>驱动方的</b>时间
        /// （白模里由 <c>ThrowSpawner.FixedUpdate</c> 用 <c>Time.fixedTime</c> / <c>Time.fixedDeltaTime</c>
        /// 统一给出）。本类只取时间，不取输入：<c>Ctx.worldInfo</c> 只服务基类的边界钳位，
        /// 而白模的敌人不被钳位（有刚体，撞物理解算），所以给一个默认值即语义正确 ——
        /// 不传某个假边界，是因为传了反而会让人以为它在生效。
        /// </remarks>
        public void Tick(float now, float deltaTime)
        {
            // 用零输入快照组装上下文：本类不吃输入，但基类的时间来源必须是驱动方那一份。
            var snapshot = new InputSnapshot(Vector2.zero, false, false, false);
            var context = new LogicContext(now, deltaTime, default, snapshot);

            FixedTick(context);

            // 禁足的递减在**帧末**，不在帧首。
            //
            // 帧首递减会让"本帧还看得见几个禁足帧"比标称少一个：`BeginStun(N)` 之后
            // 第一次 Tick 开头就把它减成 N-1，而 OnTick 读到的正是 N-1 —— 于是 N=12 实际只禁足 11 帧，
            // 第 12 帧已经回到追击分支，冲量当场被转向覆盖（实测第 11 帧速度从 5.5 掉到 3.6）。
            // 放到帧末，本帧的 OnTick 读到的就是还没被消耗的 N，`BeginStun(N)` 就等于"从被砸中那帧起禁足 N 帧"，
            // 与 BeginStun → OnTick → 递减 的直觉一致，也与测试数出来的帧数一致。
            //
            // **注意这里减完不再判**：本帧该不该禁足由 OnTick 里那次 `IsStunned` 决定，
            // 那时读到的还是本帧的值。在帧末再判一次等于把刚过去的这一帧重新定义一遍。
            if (_stunFramesRemaining > 0) _stunFramesRemaining--;
        }

        protected override void OnTick(in LogicContext ctx)
        {
            // ---- 第 1 步：把挂起的击退冲量落进本帧账本 ----
            //
            // **必须在最前面**，而且**必须在这里**（不能在 ApplyKnockback 里）：
            // FixedTick 在本方法之前的帧首已经把 _delta 清了，所以只有这一处写才是"本帧的"。
            Vector2 knockback = _pendingKnockback;
            _pendingKnockback = Vector2.zero;

            // 逐分量精确比较，不用 `knockback != Vector2.zero`：
            // `Vector2.operator!=` 带 `kEpsilon² = 1e-10` 的平方容差，即"模长小于 1e-5 就当零"。
            // 实测 3e-6 的冲量会被静默吞掉。当前的冲量量级（5.5 与 1.8）离那个门槛十几个数量级，
            // 所以今天不会触发 —— 但这是"将来有人给冲量乘一个极小系数就静默失效"的隐患。
            if (knockback.x != 0f || knockback.y != 0f) AddImpulse(knockback);

            // ---- 第 2 步：禁足 ----
            //
            // 两段合起来才是一次完整的禁足：
            //   ① 冲量那一帧**照常往下走正常提交路径**，于是它真的被写进引擎；
            //      目标速度取"本帧工作速度"（= 基类刚读到的帧首真值 + 刚落进去的冲量），
            //      衰减率传 0 ⇒ 不做指数衰减 ⇒ 冲量原样保留。
            //   ② 之后每一帧 `desired == current` ⇒ `delta = desired - current` 分量相减**恰好为 0**
            //      ⇒ `HasSubmission` 为假 ⇒ 基类不写速度 ⇒ 引擎保持匀速滑出去。
            //
            // **② 与"速度是不是零"无关**，这点很重要：成立的条件是 `desired == current`，
            // 所以敌人**正在滑行时**（速度 5.5）那一帧同样是零提交、不会去重写速度 ——
            // 否则撞墙或被别的敌人顶住时，这些重写会**覆盖物理结果**。
            // （子 agent 用 V = 5.5 / 3.6 / 1.37 / 0.5 / 1e-4 逐位验过 `SubmittedDelta` 为零。）
            //
            // **曾经写成 `if (IsStunned) return;`（纯提前返回），那是错的，而且错得很隐蔽**：
            // 冲量那一帧也是"零提交"，于是它永远不会被写进引擎 —— 现象就是"击退完全没生效"。
            // 判据是"零提交"与"确实提交了但值不变"的差别，两者观感一样、后果相反。
            if (IsStunned)
            {
                SteerTowards(Velocity, Velocity.magnitude, 0f, 0f);
                return;
            }

            if (_target == null)
            {
                // 没有目标：把速度滑停（传零方向让 SteerTowards 走指数衰减那一支）。
                SteerTowards(Vector2.zero, 0f, _enemy.Acceleration, _enemy.DecayPerSecond);
                return;
            }

            Steering steering = Steering.Resolve(
                Motor.Position,
                _target.Value,
                _enemy.StopDistance,
                _enemy.ChaseRange,
                _enemy.MaxSpeed,
                _slowMultiplier
                );

            // 停止距离之内、但玩家仍在追击范围内：**只在"快停下来了"的时候才重新压上去**。
            //
            // 这一条挡的是一个很难查的自锁：击退会把敌人推到停止距离之外一点点，
            // 而"进入停止距离就不再追"是个纯几何判据 —— 敌人一旦被推进那个区间，
            // 它自己就再也没有理由往回走了。表现是"被撞一次之后敌人黏在原地，
            // 玩家再也挨不到第二下"，不报错、也不像 bug，只是整局只掉一次血。
            //
            // 加上"速度已经很小"这个条件之后：被推着走（速度大）→ 继续滑出去；
            // 滑到快停 → 重新压上来。于是"被击退"是真正的一次脱离，而不是一次性的终局。
            //
            // 用 `Velocity` 而不是引擎回读：那是本类账本里的当帧工作速度，
            // 判据要在同一帧内自洽（引擎值滞后一个物理步）。
            if (steering.Speed <= 0f
                && Velocity.sqrMagnitude <= ResumeChaseSpeed * ResumeChaseSpeed
                && Vector2.Distance(Motor.Position, _target.Value) > _enemy.StopDistance)
            {
                Steering resume = Steering.Resolve(
                    Motor.Position,
                    _target.Value,
                    _enemy.StopDistance,
                    _enemy.ChaseRange,
                    _enemy.MaxSpeed,
                    _slowMultiplier
                    );

                if (!resume.IsIdle) steering = resume;
            }

            SteerTowards(
                steering.Direction,
                steering.Speed,
                _enemy.Acceleration,
                _enemy.DecayPerSecond
                );
        }

        /// <summary>
        /// "已经停下来了"的速度阈值（单位/秒）。
        /// </summary>
        /// <remarks>
        /// 取 0.5 而不是 0：速度是指数衰减的，永远不会<b>恰好</b>等于 0，
        /// 严格判零等于这条恢复逻辑永远不生效。也不能取太大（比如 2.0）——
        /// 那样刚被击退、速度还有一大半的时候就判为"停了"，滑行距离会明显变短。
        /// </remarks>
        private const float ResumeChaseSpeed = 0.5f;
    }
}
