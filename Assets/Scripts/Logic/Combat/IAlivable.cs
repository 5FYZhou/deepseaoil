namespace DeepseaOil.Logic.Combat
{
    /// <summary>
    /// <b>最基本的生命体征</b>：这个目标还活着吗。
    /// </summary>
    /// <remarks>
    /// <b>为什么要统一成一个接口：</b>收口前玩家侧叫 <c>IsAlive</c>、怪物侧叫 <c>IsDead</c>，
    /// 同一个问题在两个方向上表述 —— 于是每一个"谁是活的"的判据都要先查一遍"这个类型用的是哪个名字"。
    /// 命名取 <c>IsAlive</c>（球已经在用这个名字，正好作为命名依据）。
    /// <para><b>它不吃任何其它成员</b>：只有这一条属性。能受伤（<see cref="IDamageable"/>）、
    /// 能被减速（<c>ISlowEffectTarget</c>）、能被推动都是别的事 ——
    /// 将来的可推动木箱能被推、能被减速，但不该被迫实现一个空的"我受伤了"。</para>
    /// <para><b>不纳入的对象：</b><c>BallActor.IsAlive</c> 是"这个对象还在场"的存活态，
    /// 不是生命值，语义不同（见它的类注释）。<c>GameRoot</c> 的私有 <c>IsAlive(ISceneRoot)</c>
    /// 回答的是"场景根还可用吗"，同名不同义，刻意不动。</para>
    /// </remarks>
    public interface IAlivable
    {
        /// <summary>是否还活着。</summary>
        bool IsAlive { get; }
    }
}
