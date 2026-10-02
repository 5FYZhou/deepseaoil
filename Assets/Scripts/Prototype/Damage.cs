using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 一次命中的全部事实。<b>纯数据</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么是结构体而不是一个 <c>Vector2</c> 冲量：</b>受害方要的不只是"被推多远"。
    /// 现在就需要三件事 —— 落点（要算推开方向）、击退量（要进速度账本）、球种（水球留泥、土球不留）。
    /// 将来必然还要加"暴击""属性抗性""伤害数字"，那些都是加字段而不是改签名。
    /// <para><b>方向在这里算，不在受害方算：</b>"从落点指向我"这件事要用落点，
    /// 而落点只有施害方知道。让受害方去反推，等于把"落点是什么"泄露给它。</para>
    /// <para>它是 <c>readonly struct</c>：投掷结算一帧里可能造好几份，不能有堆分配。</para>
    /// </remarks>
    public readonly struct Damage
    {
        /// <summary>命中点（世界坐标，贴地）。</summary>
        public readonly Vector2 Point;

        /// <summary>从命中点指向受害者的<b>单位</b>方向；命中点正好压在受害者身上时是 <c>Vector2.up</c>。</summary>
        public readonly Vector2 Direction;

        /// <summary>击退冲量（速度，单位/秒），<b>不低于 0</b>。</summary>
        public readonly float Impulse;

        /// <summary>来源球种。</summary>
        public readonly BallType Source;

        /// <summary>来源球种对应的泥浆减速系数（<c>1</c> = 该球不留泥浆、不减速）。</summary>
        /// <remarks>
        /// 这里给的是"**系数**"而不是"要不要减速"的布尔值：将来水球分等级（大水球泥浆更黏）时，
        /// 只需换一个数，不必新增字段，也不必改 <c>MudPatch</c>。
        /// </remarks>
        public readonly float SlowMultiplier;

        /// <summary>
        /// 造成这次命中的那颗球的编号。<b>它是"同一颗球不重复结算"的判据。</b>
        /// </summary>
        /// <remarks>
        /// <b>为什么是球编号而不是"帧编号"：</b>帧编号这个判据要求"每帧有人来复位它"，
        /// 于是结算顺序就被绑死在脚本执行顺序上 —— <c>ThrowSpawner.FixedUpdate</c> 与
        /// <c>EnemyDirector.FixedUpdate</c> 谁先跑，Unity 并不保证。
        /// 先结算的那一帧里标记还没被复位，伤害会被<b>静默丢掉</b>，
        /// 表现是"有时打三下就碎、有时打四下还不碎"。
        /// <para>球编号没有这个问题：它只增不减，所以"这颗球已经结算过吗"是一个无状态的比较，
        /// 与谁先跑无关，也不需要任何复位。</para>
        /// </remarks>
        public readonly int BallId;

        /// <param name="point">命中点。</param>
        /// <param name="direction">从命中点指向受害者的单位方向。</param>
        /// <param name="impulse">击退冲量（速度）。</param>
        /// <param name="source">来源球种。</param>
        /// <param name="slowMultiplier">泥浆减速系数。</param>
        /// <param name="ballId">来源球编号。</param>
        public Damage(
            Vector2 point,
            Vector2 direction,
            float impulse,
            BallType source,
            float slowMultiplier,
            int ballId)
        {
            Point = point;
            Direction = direction;
            Impulse = impulse;
            Source = source;
            SlowMultiplier = slowMultiplier;
            BallId = ballId;
        }

        /// <summary>
        /// 按球种与落点造一次命中。<b>静态纯函数</b>：所有"土球打得远、水球留泥"的差异只写在这一处。
        /// </summary>
        /// <param name="point">落点（世界坐标，贴地）。</param>
        /// <param name="victim">受害者当前位置。</param>
        /// <param name="type">球种。</param>
        /// <param name="slowMultiplier">该球的泥浆减速系数（不给减益时传 <c>1</c>）。</param>
        /// <param name="ballId">来源球编号；只用于去重，不参与计算。</param>
        /// <remarks>
        /// <b>两种球的差异只有一个比例，不是"有或没有"：</b>土球全额击退，水球按
        /// <see cref="ThrowConstants.WATER_KNOCKBACK_SCALE"/> 折减 —— 于是"土球打退、水球留泥"
        /// 是幅度上的偏好，水球因此仍有打击感。取 0 会让水球打上去像没打中。
        /// <para><b>正中命中时给"上"：</b>落点正好压在受害者身上时方向向量为零，
        /// 归一化会产生 <c>NaN</c>（球会带着非数坐标消失）。给一个确定方向比到处判 <c>NaN</c> 便宜，
        /// 也不该让站在落点正中的敌人免疫击退。</para>
        /// </remarks>
        public static Damage For(Vector2 point, Vector2 victim, BallType type, float slowMultiplier, int ballId)
        {
            Vector2 delta = victim - point;

            Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

            float knockback = type == BallType.Water
                ? ThrowConstants.ENEMY_KNOCKBACK_IMPULSE * ThrowConstants.WATER_KNOCKBACK_SCALE
                : ThrowConstants.ENEMY_KNOCKBACK_IMPULSE;

            // 与泥浆同向：泥浆里被推得也近（见 EnemyConfig.ScaledKnockback 的注释）。
            float impulse = EnemyConfig.ScaledKnockback(knockback, slowMultiplier);

            return new Damage(point, direction, impulse, type, slowMultiplier, ballId);
        }
    }
}
