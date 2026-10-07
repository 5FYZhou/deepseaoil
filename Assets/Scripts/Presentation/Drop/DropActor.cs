using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>
    /// 掉落物实体基类：<b>抛物线 → 等待被拾取 → 追踪 → 已领取</b>四个阶段，
    /// 外加"被玩家碰到就交给子类决定"的那一步。
    /// </summary>
    /// <remarks>
    /// <b>与投掷球（<c>BallActor</c>）平行，不互相继承</b>（审查已定）：球是"玩家抛出、有球定义、
    /// 落地结算"，掉落物是"世界产出、抛物线、被领走只发一条事实"。两者共享的只是零件
    /// （图元、由上层驱动、不自驱 <c>Update</c> 的纪律）。
    /// <para><b>它不自驱、也不自毁：</b>由 <c>DropDirector</c> 每渲染帧推进，领取后由持有者回收 ——
    /// 与球同一条纪律（回收权归持有者，两个地方都能销毁就会出现"谁先谁后"的竞态）。</para>
    /// <para><b>资产与形状由子类自持</b>（<see cref="BuildBody"/>）：图元、颜色、碰撞半径都是
    /// "这一种掉落物长什么样"，不该由持有者或取值定义决定。</para>
    /// <para><b>阶段为什么用枚举而不是几个 bool：</b>收口前是 <c>_flyingToPlayer</c> ＋ <c>_collected</c>
    /// 两个标志，四个阶段靠它们组合推断（"在飞但没被领取"＝等触发）。加一个阶段就要再加一个 bool，
    /// 而"两个 bool 同时为真"这种状态永远是 bug。</para>
    /// </remarks>
    public abstract class DropActor : MonoBehaviour, IDrivenEntity
    {
        /// <summary>阶段。<b>子类只在 <see cref="TickFalling"/> / <see cref="TickWaiting"/> / <see cref="TickHoming"/> 里做事。</b></summary>
        protected enum Phase
        {
            /// <summary>抛物线飞行中。</summary>
            Falling,

            /// <summary>落在落点上，等玩家来碰。</summary>
            Waiting,

            /// <summary>被碰到了，正在飞向玩家。</summary>
            Homing,

            /// <summary>已被领取（事件已发，等持有者回收）。</summary>
            Done,
        }

        /// <summary>抛物线已飞时长（秒）；由 <see cref="AdvanceFalling"/> 推进。</summary>
        private float _elapsed;

        /// <summary>本次掉落物的取值边界（数值）。</summary>
        protected DropSpec Definition { get; private set; }

        /// <summary>本次掉落物的种类（名字牌：领取时的载荷与实体工厂都要它）。</summary>
        protected DropType Type { get; private set; }

        /// <summary>当前阶段。</summary>
        protected Phase CurrentPhase { get; private set; }

        /// <summary>玩家（装配期注入；被碰到时刷新成"碰到我的那个玩家"）。</summary>
        protected Transform Player { get; private set; }

        /// <summary>生成点（抛物线起点）。</summary>
        protected Vector2 Origin { get; private set; }

        /// <summary>落点（抛物线终点，也是"等待被拾取"时所在的位置）。</summary>
        protected Vector2 Landing { get; private set; }

        /// <summary>是否已经被领取。</summary>
        public bool IsCollected => CurrentPhase == Phase.Done;

        /// <summary>是否还在场（未被领取即在场）。<see cref="IDrivenEntity"/> 的口径。</summary>
        public bool IsAlive => CurrentPhase != Phase.Done;

        /// <summary>当前阶段的读数（诊断用；<c>0=Falling 1=Waiting 2=Homing 3=Done</c>）。</summary>
        /// <remarks>
        /// <b>它是"阶段枚举降级成 int"，这是刻意的</b>：读它的人（调试面板）要的是"第几档"这种
        /// 一眼能比较的量，而不是再认识一个枚举类型。语义对照写在上面这一行里。
        /// </remarks>
        public int PhaseCode => (int)CurrentPhase;

        /// <summary>回收本实体（销毁自己的 GameObject）。<b>幂等</b>：重复调用不会重复销毁。</summary>
        /// <remarks>所有权归持有者（<c>DropDirector</c>）：本方法只是"被回收"的实现，
        /// 调用时机由持有者决定 —— 与 <c>BallActor.Dispose</c> 同一条纪律。</remarks>
        public void Dispose()
        {
            if (this == null) return;

            Destroy(gameObject);
        }

        /// <summary>
        /// 组装：写数值、摆位置、建出自己需要的组件与观感。
        /// </summary>
        /// <param name="request">产出请求（种类 ＋ 起点 ＋ 落点）。</param>
        /// <param name="type">掉落物种类。</param>
        /// <param name="definition">取值边界（数值）。</param>
        /// <param name="player">玩家引用（持有者注入）。</param>
        public void Initialize(in DropSpawnRequest request, DropType type, DropSpec definition, Transform player)
        {
            Type = type;
            Definition = definition;
            Player = player;
            Origin = request.Origin;
            Landing = request.Landing;
            _elapsed = 0f;
            CurrentPhase = Phase.Falling;

            transform.position = new Vector3(request.Origin.x, request.Origin.y, 0f);

            BuildBody();

            OnPhaseEntered(Phase.Falling);
        }

        /// <summary>推进一个渲染帧（由 <c>DropDirector</c> 调；暂停时 <c>dt = 0</c>，掉落物自然冻结）。</summary>
        public void Tick(float deltaTime)
        {
            switch (CurrentPhase)
            {
                case Phase.Falling:
                    TickFalling(deltaTime);
                    break;

                case Phase.Waiting:
                    TickWaiting(deltaTime);
                    break;

                case Phase.Homing:
                    TickHoming(deltaTime);
                    break;
            }
        }

        /// <summary>建出自己需要的组件与观感（碰撞体 / 图元 / 颜色 / 尺寸）。<b>资产由实体自持</b>。</summary>
        protected abstract void BuildBody();

        /// <summary>抛物线阶段。</summary>
        protected abstract void TickFalling(float deltaTime);

        /// <summary>等待被拾取阶段。<b>默认什么都不做</b>（站着等人来碰）。</summary>
        protected virtual void TickWaiting(float deltaTime)
        {
        }

        /// <summary>追踪阶段。</summary>
        protected abstract void TickHoming(float deltaTime);

        /// <summary>进入某阶段时的一次性动作（子类按需覆写）。</summary>
        protected virtual void OnPhaseEntered(Phase phase)
        {
        }

        /// <summary>玩家碰到了本掉落物（子类决定"被吸走 / 被踩碎 / 没反应"）。</summary>
        protected virtual void OnPlayerReached(Transform player)
        {
        }

        /// <summary>
        /// 抛物线进度：推进计时并返回 <c>0..1</c> 的 <c>t</c>。
        /// </summary>
        /// <remarks><b>"时长为 0 就直接落地"这条护栏只写在这里</b>：让每个子类各写一次，
        /// 迟早会有一处除出非数坐标（球那边栽过同一个跟头）。</remarks>
        protected float AdvanceFalling(float deltaTime)
        {
            float duration = Definition.FlightDuration;

            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            _elapsed += deltaTime;

            return t;
        }

        /// <summary>切换阶段（<b>唯一入口</b>：保证"进入"回调一定被调到）。</summary>
        protected void EnterPhase(Phase phase)
        {
            CurrentPhase = phase;

            OnPhaseEntered(phase);
        }

        /// <summary>
        /// 领取：进入 <see cref="Phase.Done"/> 并发一条事实事件。<b>回收不在这里</b>（归持有者）。
        /// </summary>
        /// <remarks>
        /// 载荷带"是什么 ＋ 几个"（数量来自取值边界）：订阅方按类型裁决给玩家什么，
        /// 而不是由掉落物直接去改玩家账本 —— 世界 → 玩家只有"通知"一条路。
        /// </remarks>
        protected void Collect()
        {
            if (CurrentPhase == Phase.Done) return;

            EnterPhase(Phase.Done);

            EventBus<DropCollected>.Publish(new DropCollected(Type, Definition.Amount));
        }

        /// <summary>
        /// 玩家碰到本掉落物（掉落物是触发器，玩家有刚体 ⇒ 回调会到两边）。
        /// </summary>
        /// <remarks>
        /// <b>Enter 与 Stay 都要听：</b>玩家一直站在掉落物上时 <c>Enter</c> 只发生一次 ——
        /// 收口前只听了 Enter，于是"追踪目标消失后回到等待"的那颗球会永远躺在玩家脚下不响应。
        /// <para>只认 <c>PlayerController</c> 而不是某种 Tag：Tag 是一处需要人工同步的工程设置。</para>
        /// <para><b>追踪点取玩家组合根</b>，不取"哪个碰撞体先碰到"：玩家有多个子碰撞体时，
        /// 后者会把追踪点漂到某个子物体上（收口前 <c>WaterBall</c> 取的就是碰撞体宿主）。</para>
        /// </remarks>
        private void OnTriggerEnter2D(Collider2D other)
        {
            TryPlayerReached(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryPlayerReached(other);
        }

        private void TryPlayerReached(Collider2D other)
        {
            if (CurrentPhase == Phase.Done || CurrentPhase == Phase.Homing) return;

            PlayerController controller = other.GetComponentInParent<PlayerController>();

            if (controller == null) return;

            Player = controller.transform;

            OnPlayerReached(Player);
        }
    }
}
