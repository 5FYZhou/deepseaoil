namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 场景级组合根的注册契约：<b>它自己向 <c>GameRoot</c> 报到，而不是被 Inspector 拖进去</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么要有它：</b>收口之前，<c>GameRoot</c> 靠一个 <c>[SerializeField] CombatRoot combat</c>
    /// 认识战斗切片 —— "谁被谁驱动"这件事藏在场景文件的序列化数据里，问答只能靠翻场景。
    /// 改成注册制之后，答案变成一句话：<b>场景根在自己 <c>Start</c> 里注册，<c>GameRoot</c> 按
    /// <see cref="Order"/> 驱动它们，销毁时自己注销</b>。
    /// <para><b>为什么拆成三个接口：</b>一个场景根可能只需要物理帧（玩家），也可能两帧都要（战斗）。
    /// 合成一个接口就得给不需要的那一半写空实现 —— 空实现既是死代码，又会让"这一帧谁被跑了"
    /// 这个问题重新变得不可读。所以：<see cref="ISceneRoot"/> 只回答"我是谁、排第几"，
    /// 需要被驱动的帧各自再实现 <see cref="IRenderTicked"/> / <see cref="IPhysicsTicked"/>。</para>
    /// <para><b>顺序由 <see cref="Order"/> 决定，不由注册先后决定：</b>注册发生在
    /// <c>Start</c>，而多个 <c>MonoBehaviour</c> 的 <c>Start</c> 先后是 Unity 的自由 ——
    /// 靠注册顺序等于把帧内顺序交给引擎抽签。小者先跑。</para>
    /// </remarks>
    public interface ISceneRoot
    {
        /// <summary>驱动顺序，小者先。约定：玩家侧 <c>-100</c>、世界侧 <c>0</c>。</summary>
        int Order { get; }
    }

    /// <summary>需要被渲染帧驱动的场景根（由 <c>GameRoot.Update</c> 调）。</summary>
    public interface IRenderTicked
    {
        /// <param name="deltaTime"><c>Time.deltaTime</c>；暂停（<c>timeScale = 0</c>）时为 0。</param>
        void RenderTick(float deltaTime);
    }

    /// <summary>需要被物理帧驱动的场景根（由 <c>GameRoot.FixedUpdate</c> 调）。</summary>
    /// <remarks>暂停时 Unity 不跑 <c>FixedUpdate</c>，所以这里不需要再挡一层。</remarks>
    public interface IPhysicsTicked
    {
        /// <param name="deltaTime"><c>Time.fixedDeltaTime</c>。</param>
        void FixedTick(float deltaTime);
    }
}
