using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>一个格子状态的取值边界：持有 <c>tile_state</c> 表行，暴露被消费的语义点。</summary>
    /// <remarks>"状态转换时产生一次基础伤害"的数值在 <see cref="EnterDamage"/>：它属于状态而非球种，换球触发同一状态伤害口径不变。行不对外暴露。</remarks>
    public sealed class TileStateSpec
    {
        private readonly TileState _row;

        public TileStateSpec(TileState row)
        {
            _row = row;
        }

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
