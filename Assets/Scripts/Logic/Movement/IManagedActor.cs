namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// <b>自持速度账本</b>的角色标记：物理层的"推一下"必须跳过它们。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要它：</b>落地冲量由表现层的执行者施加给半径内的<b>无生命</b>刚体，
    /// 而角色速度由各自的账本写 —— 不跳的话，<c>AddForce</c> 会在账本写出速度之后<b>又写一次</b>，
    /// 每帧两个速度写者。
    /// <para><b>收口前它是一份硬编码名单</b>（<c>GetComponentInParent&lt;EnemyActor&gt;()</c> /
    /// <c>&lt;PlayerController&gt;()</c>）：将来加一个自带速度账本的角色时，漏改的表现是
    /// "被推了两次速度"，而且<b>完全静默</b>。改成标记接口之后，新角色实现它即被跳过 ——
    /// 这条判据与"统一生命体征用 <c>IAlivable</c>"是同一招。</para>
    /// <para><b>它刻意是空接口</b>：唯一的语义就是"别推我"。</para>
    /// </remarks>
    public interface IManagedActor
    {
    }
}
