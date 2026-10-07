namespace DeepseaOil.Logic.Movement
{
    /// <summary>自持速度账本的角色标记，空接口。</summary>
    /// <remarks>
    /// 物理层的"推一下"必须跳过这些角色，否则 <c>AddForce</c> 会在账本写出速度之后又写一次，
    /// 每帧两个速度写者，且完全静默；新增自带速度账本的角色必须实现本接口。
    /// </remarks>
    public interface IManagedActor
    {
    }
}
