namespace DeepseaOil.Foundation
{
    /// <summary>
    /// 状态机能看见的那几个<b>运动标量</b>：地基认识的全部角色数据到此为止。
    /// </summary>
    /// <remarks>
    /// <b>为什么不是 <c>CharacterConfig</c>：</b>收口前 <see cref="IStateHost"/> 直接暴露
    /// <c>DeepseaOil.Data.CharacterConfig</c>，于是工程的地基（<c>Foundation</c>，其余四层都依赖它）
    /// 反向认识数据层 —— 这是全工程唯一的逆向层依赖，而它不报错、不警告、测试也不查
    /// （没有 asmdef 时层与层之间没有编译器边界）。查出来的时候它的实际用途只有本结构里的六个值。
    /// <para><b>为什么是标量包而不是窄接口：</b>本结构<b>没有行为</b>，只是六个一次装配定值的数。
    /// 窄接口会要求每个宿主实现六个成员，而宿主本来就是从同一份配置折算出来的；
    /// 结构体则让"折算"只发生在一个地方（装配期的 <c>Configure</c>）。</para>
    /// <para><b>不在这里的字段就是没有消费者。</b><c>snapToEightDirections</c> 的消费在
    /// <c>PlayerController</c>（输入吸附），<c>extraForceScale</c> 的消费在账本的外力累加 ——
    /// 两者都不经状态机，所以都不该出现在地基里。加字段前先确认真的有一个状态要读它。</para>
    /// </remarks>
    public readonly struct MotionParams
    {
        /// <summary>没有配置时的取值：全零 ⇒ 零惯性、零速度、零冲刺时长。</summary>
        public static readonly MotionParams None = default;

        /// <summary>移动速度（单位/秒）。俯视角零惯性角色：这是速度，不是加速度。</summary>
        public readonly float MoveSpeed;

        /// <summary>
        /// 加速度（单位/秒²）。<b><c>≤ 0</c> 是"零惯性"的判据</b>：
        /// 于是"要不要惯性"是一个配置问题，而不是一次代码改动（见 <c>ActorLedger.MoveTowards</c>）。
        /// </summary>
        public readonly float MoveAcceleration;

        /// <summary>反向输入的转向衰减率（1/秒），越大转身越快。</summary>
        public readonly float TurnDecayRate;

        /// <summary>冲刺速度（单位/秒）。</summary>
        public readonly float DashSpeed;

        /// <summary>冲刺持续时长（秒）。</summary>
        public readonly float DashDuration;

        /// <summary>受击滑停的减速度（单位/秒²）；<c>≤ 0</c> 时由消费方落回 <see cref="MoveAcceleration"/>。</summary>
        public readonly float HurtDecay;

        /// <param name="moveSpeed">见 <see cref="MoveSpeed"/>。</param>
        /// <param name="moveAcceleration">见 <see cref="MoveAcceleration"/>。</param>
        /// <param name="turnDecayRate">见 <see cref="TurnDecayRate"/>。</param>
        /// <param name="dashSpeed">见 <see cref="DashSpeed"/>。</param>
        /// <param name="dashDuration">见 <see cref="DashDuration"/>。</param>
        /// <param name="hurtDecay">见 <see cref="HurtDecay"/>。</param>
        public MotionParams(
            float moveSpeed,
            float moveAcceleration,
            float turnDecayRate,
            float dashSpeed,
            float dashDuration,
            float hurtDecay)
        {
            MoveSpeed = moveSpeed;
            MoveAcceleration = moveAcceleration;
            TurnDecayRate = turnDecayRate;
            DashSpeed = dashSpeed;
            DashDuration = dashDuration;
            HurtDecay = hurtDecay;
        }
    }
}
