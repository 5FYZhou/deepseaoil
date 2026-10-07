using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>掉落物实体基类：<b>抛物线 → 等待被拾取 → 追踪 → 已领取</b>四个阶段，外加"被玩家碰到就交给子类决定"的那一步。</summary>
    /// <remarks>不自驱、也不自毁：由 <c>DropDirector</c> 每渲染帧推进，领取后由持有者回收（回收权归持有者）；资产与形状由子类自持（<see cref="BuildBody"/>）。</remarks>
    public abstract class DropActor : MonoBehaviour, IDrivenEntity
    {
        /// <summary>阶段：子类只在 <see cref="TickFalling"/> / <see cref="TickWaiting"/> / <see cref="TickHoming"/> 里做事。</summary>
        protected enum Phase
        {
            Falling,

            Waiting,

            Homing,

            Done,
        }

        private float _elapsed;

        protected DropSpec Definition { get; private set; }

        protected DropType Type { get; private set; }

        protected Phase CurrentPhase { get; private set; }

        protected Transform Player { get; private set; }

        protected Vector2 Origin { get; private set; }

        protected Vector2 Landing { get; private set; }

        public bool IsCollected => CurrentPhase == Phase.Done;

        /// <summary>未被领取即在场（<see cref="IDrivenEntity"/> 的口径）。</summary>
        public bool IsAlive => CurrentPhase != Phase.Done;

        /// <summary>当前阶段的读数（诊断用；<c>0=Falling 1=Waiting 2=Homing 3=Done</c>）。</summary>
        public int PhaseCode => (int)CurrentPhase;

        public void Dispose()
        {
            if (this == null) return;

            Destroy(gameObject);
        }

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

        protected abstract void BuildBody();

        protected abstract void TickFalling(float deltaTime);

        protected virtual void TickWaiting(float deltaTime)
        {
        }

        protected abstract void TickHoming(float deltaTime);

        /// <summary>进入某阶段时的一次性动作（子类按需覆写）。</summary>
        protected virtual void OnPhaseEntered(Phase phase)
        {
        }

        protected virtual void OnPlayerReached(Transform player)
        {
        }

        /// <summary>抛物线进度：推进计时并返回 <c>0..1</c> 的 <c>t</c>。</summary>
        /// <remarks><b>"时长为 0 就直接落地"这条护栏只写在这里</b>：让每个子类各写一次，迟早会有一处除出非数坐标（球那边栽过同一个跟头）。</remarks>
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

        /// <remarks>载荷带"是什么 ＋ 几个"：订阅方按类型裁决给玩家什么，而不是由掉落物直接去改玩家账本。</remarks>
        protected void Collect()
        {
            if (CurrentPhase == Phase.Done) return;

            EnterPhase(Phase.Done);

            EventBus<DropCollected>.Publish(new DropCollected(Type, Definition.Amount));
        }

        // 掉落物是触发器，玩家有刚体 ⇒ 回调会到两边。Enter 与 Stay 都要听：
        // 玩家一直站在掉落物上时 Enter 只发生一次；追踪点取玩家组合根（不取碰撞体宿主），只认 PlayerController 而不是 Tag。
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
