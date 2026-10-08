namespace DeepseaOil.Foundation
{
    /// <summary>状态机能看见的运动标量：地基认识的全部角色数据到此为止</summary>
    /// <remarks>不能引用 CharacterConfig：反向依赖由编译器挡下；装配期 Configure 一次性折算，事后改 SO 不生效</remarks>
    public readonly struct MotionParams
    {
        public static readonly MotionParams None = default;

        /// <summary>移动速度（单位/秒）；俯视角零惯性角色用</summary>
        public readonly float MoveSpeed;

        /// <summary>加速度（单位/秒²）；≤ 0 即"零惯性"判据</summary>
        public readonly float MoveAcceleration;

        /// <summary>反向输入的转向衰减率（1/秒），越大转身越快</summary>
        public readonly float TurnDecayRate;

        public readonly float DashSpeed;

        /// <summary>冲刺持续时长（秒）；本结构衰减率 1/秒、加速度 单位/秒²</summary>
        public readonly float DashDuration;

        /// <summary>受击滑停减速度（单位/秒²）；≤ 0 落回 MoveAcceleration</summary>
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
