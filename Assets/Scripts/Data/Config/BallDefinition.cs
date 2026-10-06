using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 一个球种的<b>全部数值</b> —— 它是"这个球是什么"的唯一取值边界：
    /// 逻辑层与表现层都只认它，谁也不直接读表或 SO。
    /// </summary>
    /// <remarks>
    /// <b>折算只发生在 <see cref="SpecCatalog"/> 一处</b>：于是"表里加一列"的影响面是
    /// "一个结构体 + 一个折算点"，而不是散落在所有消费者里。
    /// <para><b>为什么叫 <c>Definition</c> 而不是 <c>Spec</c>：</b>审查把"以球为键的单一真源"
    /// 定为 <c>BallDefinition</c>，并明确"这层取值接口现在就要有，哪怕眼下它只是转发" ——
    /// 将来取值来源若从"表 ＋ 代码"变成"表 ＋ SO"，改的是它，不是每一个消费者。</para>
    /// <para><b>观感资产不在这里</b>（贴图 / 颜色 / 预制体）：那些由表现层的球实体自持
    /// （白模阶段是程序化图元，见 <c>BallActor</c> / <c>BallView</c>）。把 <c>Sprite</c> 之类的引用
    /// 塞进本结构体会让取值边界变成"逻辑层也认识美术资产"。</para>
    /// </remarks>
    public readonly struct BallDefinition
    {
        /// <summary>球种（= 表主键）。</summary>
        public readonly BallType Type;

        /// <summary>显示名。</summary>
        public readonly string Name;

        /// <summary>飞行参数（时长 / 弧高 / 距离上下限）。</summary>
        /// <remarks>射程（<see cref="ThrowSpec.MinThrowDistance"/> / <c>MaxThrowDistance</c>）
        /// 属<b>玩家侧资格</b>，不属球的"世界规则" —— 这一点由消费者区分，不由本结构体区分。</remarks>
        public readonly ThrowSpec Throw;

        /// <summary>落地后目标格转成的状态；<see cref="TileStateType.Normal"/> = 不改格子。</summary>
        public readonly TileStateType TileState;

        /// <param name="type">球种（表主键）。</param>
        /// <param name="name">显示名。</param>
        /// <param name="throwSpec">飞行参数。</param>
        /// <param name="tileState">落地后目标格的状态。</param>
        public BallDefinition(BallType type, string name, in ThrowSpec throwSpec, TileStateType tileState)
        {
            Type = type;
            Name = name;
            Throw = throwSpec;
            TileState = tileState;
        }

        /// <summary>
        /// 落地是否真的改变世界状态。
        /// </summary>
        /// <remarks>
        /// <b>它是配置事实，不是代码里的一个 <c>if</c>：</b>"土球落地什么都不做"由
        /// <c>projectile.tile_state = Normal</c> 表达。表现层的冲量与逻辑层的改格共用这一个判据，
        /// 于是"换一种球"不会出现两处判断不一致。
        /// </remarks>
        public bool HasLandingEffect => TileState != TileStateType.Normal;
    }
}
