using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个格子状态的取值边界：持有 <c>tile_state</c> 表行，暴露被消费的语义点。
    /// </summary>
    /// <remarks>
    /// <b>"状态转换时产生一次基础伤害"这条全局设定的数值就在这里</b>（<see cref="EnterDamage"/>）：
    /// 它是状态的属性而不是某个球种的属性 —— 换一种球触发同一个状态，伤害口径不变。
    /// <para>行不对外暴露（见 <see cref="ProjectileSpec"/> 的同一条纪律）。</para>
    /// </remarks>
    public sealed class TileStateSpec
    {
        private readonly TileState _row;

        /// <param name="row">表行（<c>tile_state</c>）。</param>
        public TileStateSpec(TileState row)
        {
            _row = row;
        }

        /// <summary>状态 ID（= 表主键）。</summary>
        public TileStateType Id => _row.Id;

        /// <summary>踩在上面的速度系数（<c>1</c> = 不减速）。</summary>
        public float SlowFactor => _row.SlowFactor;

        /// <summary>持续时间（秒）；<c>0</c> = 永久（只能被别的状态顶掉）。</summary>
        public float Duration => _row.Duration;

        /// <summary>进入该状态时对该格敌人的伤害；<c>0</c> = 不伤害。</summary>
        public float EnterDamage => _row.EnterDamage;

        /// <summary>进入该状态时对该格敌人的击退冲量；<c>0</c> = 不击退。</summary>
        public float EnterKnockback => _row.EnterKnockback;
    }
}
