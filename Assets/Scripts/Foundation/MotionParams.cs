namespace DeepseaOil.Foundation
{
    /// <summary>状态机能看见的那几个<b>运动标量</b>：地基认识的全部角色数据到此为止。</summary>
    /// <remarks><b>为什么不是 <c>CharacterConfig</c>：</b>那会让全工程的地基（<c>Foundation</c>，其余四层都依赖它）反向依赖数据层。这种逆向层依赖现在由编译器直接挡下：<c>DeepseaOil.Foundation.asmdef</c> 的 references 是空的，碰 Data 就是 CS0234。
    /// <b>这里是装配期 <c>Configure</c> 一次性折算的快照：事后改 SO 不生效</b>（M19/M20 测试因断言"改 SO 即时生效"被删）；不在这里的字段就是没有消费者，加字段前先确认真的有一个状态要读它。</remarks>
    public readonly struct MotionParams
    {
        /// <summary>没有配置时的取值：全零 ⇒ 零惯性、零速度、零冲刺时长。</summary>
        public static readonly MotionParams None = default;

        /// <summary>移动速度（单位/秒）。俯视角零惯性角色：这是速度，不是加速度。</summary>
        public readonly float MoveSpeed;

        /// <summary>加速度（单位/秒²）。<b><c>≤ 0</c> 是"零惯性"的判据</b>：于是"要不要惯性"是一个配置问题，而不是一次代码改动。</summary>
        public readonly float MoveAcceleration;

        /// <summary>反向输入的转向衰减率（1/秒），越大转身越快。</summary>
        public readonly float TurnDecayRate;

        public readonly float DashSpeed;

        /// <summary>冲刺持续时长（秒）；本结构的衰减率一律 1/秒、加速度一律 单位/秒²。</summary>
        public readonly float DashDuration;

        /// <summary>受击滑停的减速度（单位/秒²）；<c>≤ 0</c> 时由消费方落回 <see cref="MoveAcceleration"/>。</summary>
        public readonly float HurtDecay;

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
