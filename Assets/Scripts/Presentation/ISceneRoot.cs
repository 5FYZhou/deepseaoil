namespace DeepseaOil.Presentation
{
    // ─────────────────────────────────────────────────────────────────────────
    // 本层的命名空间约定（此文件是它的落点，因为它是本层的根契约）
    //
    // **一个子目录一个命名空间**：`Presentation/Actor` → `DeepseaOil.Presentation.Actor`，
    // 依此类推。理由不是审美：本工程**没有 asmdef**，编译器眼里只有程序集，
    // 于是命名空间是层与层、组与组之间唯一的边界表达。有一半文件用父级、一半用子级的话，
    // "谁能看见谁"在代码里就分不出来（那正是本约定被补写下来的原因）。
    //
    // 三处**平坦**（刻意留在 `DeepseaOil.Presentation`，不是漏改）：
    //   · `Presentation/Root/`   —— 组合根们（GameRoot / PlayerController / CombatRoot / GameManager）
    //     与 `ISceneRoot` / `IDrivenEntity` 同属"本层的入口"，加一层名字只会让注入点变啰嗦；
    //   · `Presentation/UI/Panel/` —— 面板全是 `Presentation.UI` 的实现细节，UI 已是子级；
    //   · `Presentation/` 根下两个接口文件。
    // 除这三处，**新增目录必须带自己的命名空间**。
    //
    // 反例留档：`Presentation/Debug/` 曾按目录取名为 `DeepseaOil.Presentation.Debug` ——
    // 那会让本层所有 `Debug.LogError(...)` 被解析成"该命名空间下没有 LogError"（CS0234），
    // 一次改动引发 61 处编译错误。目录因此改名 `Diagnostics`（命名空间同理），
    // 而不是给 `Debug` 开一条"可以用父级"的例外。
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 场景根的驱动顺序档位。<b>顺序是契约，不是魔法数字</b>。
    /// </summary>
    /// <remarks>
    /// 收口前 <c>-100</c> / <c>0</c> 是两个字面量，约定只写在一句注释里 ——
    /// 于是一个新场景根本无从知道"我该排第几"，而排错的表现是"这一帧的接触判定读到了旧位置"。
    /// 抽成常量之后，档位是有名字的、可搜索的，也顺带说明了每一档的理由。
    /// <para>档位之间的空隙是刻意的：要插一个新的场景根时，取相邻两档的中点即可
    /// （或在中间新增一档并把理由写在这里）。</para>
    /// </remarks>
    public static class SceneOrder
    {
        /// <summary>
        /// 玩家侧：<b>必须早于世界侧</b>（先提交速度、先读输入）。
        /// </summary>
        /// <remarks>世界侧的接触判定读的是"玩家这一帧提交后的位置"，顺序反了就会用到上一帧的站位。</remarks>
        public const int Player = -100;

        /// <summary>世界侧：格子 / 球 / 掉落物 / 敌人 / 喷泉。</summary>
        public const int World = 0;
    }

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
        /// <summary>驱动顺序，小者先。档位见 <see cref="SceneOrder"/>。</summary>
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
