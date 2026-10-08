using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <remarks>不自驱也不自毁，由 DropDirector 每帧推进，领取后由持有者回收</remarks>
    public abstract class DropActor : MonoBehaviour, IDrivenEntity
    {
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

        /// <summary>未被领取即在场</summary>
        public bool IsAlive => CurrentPhase != Phase.Done;

        /// <summary>诊断用整数值；0=Falling 1=Waiting 2=Homing 3=Done</summary>
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

        /// <summary>进入某阶段时的一次性动作</summary>
        protected virtual void OnPhaseEntered(Phase phase)
        {
        }

        protected virtual void OnPlayerReached(Transform player)
        {
        }

        /// <remarks>"时长为 0 就直接落地"护栏只写在这里：各子类自己写迟早除出非数坐标</remarks>
        protected float AdvanceFalling(float deltaTime)
        {
            float duration = Definition.FlightDuration;

            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            _elapsed += deltaTime;

            return t;
        }

        /// <summary>唯一入口：保证进入回调一定被调到</summary>
        protected void EnterPhase(Phase phase)
        {
            CurrentPhase = phase;

            OnPhaseEntered(phase);
        }

        /// <remarks>载荷带是什么＋几个：订阅方按类型裁决给玩家什么，掉落物不直接改账本</remarks>
        protected void Collect()
        {
            if (CurrentPhase == Phase.Done) return;

            EnterPhase(Phase.Done);

            EventBus<DropCollected>.Publish(new DropCollected(Type, Definition.Amount));
        }

        // 掉落物是触发器、玩家有刚体 ⇒ 回调到两边，Enter 与 Stay 都要听：玩家一直站在上面时 Enter 只发生一次。追踪点取玩家组合根，只认 PlayerController
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
