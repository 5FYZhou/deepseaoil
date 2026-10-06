namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 场景级组合根的注册契约：<b>它自己向 <c>GameRoot</c> 报到，而不是被 Inspector 拖进去</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么要有它：</b>收口之前，<c>GameRoot</c> 靠一个 <c>[SerializeField] CombatRoot combat</c>
    /// 认识战斗切片 —— "谁被谁驱动"这件事藏在场景文件的序列化数据里，问答只能靠翻场景。
    /// 改成注册制之后，答案变成一句话：<b>场景根在自己 <c>Start</c> 里注册，
    /// <c>GameRoot</c> 按 <see cref="Order"/> 先逐个 <see cref="Attach"/>、再驱动，销毁时自己注销</b>。
    /// <para><b>为什么拆成三个接口：</b>一个场景根可能只需要物理帧（玩家），也可能两帧都要（战斗）。
    /// 合成一个接口就得给不需要的那一半写空实现 —— 空实现既是死代码，又会让"这一帧谁被跑了"
    /// 这个问题重新变得不可读。所以：<see cref="ISceneRoot"/> 只回答"我是谁、排第几、怎么装配"，
    /// 需要被驱动的帧各自再实现 <see cref="IRenderTicked"/> / <see cref="IPhysicsTicked"/>。</para>
    /// <para><b>顺序由 <see cref="Order"/> 决定，不由注册先后决定：</b>注册发生在
    /// <c>Start</c>，而多个 <c>MonoBehaviour</c> 的 <c>Start</c> 先后是 Unity 的自由 ——
    /// 靠注册顺序等于把帧内顺序交给引擎抽签。小者先跑。</para>
    /// </remarks>
    public interface ISceneRoot
    {
        /// <summary>驱动顺序，小者先。约定：玩家侧 <c>-100</c>、世界侧 <c>0</c>。</summary>
        int Order { get; }

        /// <summary>
        /// 装配：<b>由 <c>GameRoot</c> 在第一个被驱动的帧按 <see cref="Order"/> 调一次</b>。
        /// </summary>
        /// <remarks>
        /// <b>为什么装配不能放在 <c>Awake</c> / <c>Start</c>：</b>
        /// <list type="bullet">
        /// <item><c>Awake</c> —— 组件之间的顺序 Unity 不保证，而装配要读 <c>ConfigModule</c>
        /// （由 <c>GameRoot.Awake</c> 装配）：排在它前面就是一次"看运气"的启动崩溃；</item>
        /// <item><c>Start</c> —— 它虽然一定晚于所有 <c>Awake</c>（配表已就绪），
        /// 但<b>多个组件之间的 <c>Start</c> 同样无序</b>：世界侧装配时要读玩家侧的 <c>Logic</c>，
        /// 谁先跑还是看运气。</item>
        /// </list>
        /// <para>交给组合根按 <see cref="Order"/> 逐个调，两件事同时有答案：
        /// 配表一定就绪，且"谁先装配"变成代码里的一个数字。</para>
        /// <para><b>必须幂等</b>：<c>FixedUpdate</c> 可能先于第一个 <c>Update</c> 跑，
        /// 两个通道都会请求装配，而每个场景根只该被装配一次。</para>
        /// </remarks>
        void Attach();
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
