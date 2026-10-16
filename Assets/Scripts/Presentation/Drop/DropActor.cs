using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using DeepseaOil.Logic.Events;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Drop
{
    /// <remarks>不自驱也不自毁，由 DropDirector 每帧推进</remarks>
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

        // Q 键丢弃用：冷却期内忽略接触，防自吸
        private float _pickupCooldown;

        protected DropSpec Definition { get; private set; }

        public DropType Type { get; private set; }

        public SeedType Seed { get; private set; }

        protected Phase CurrentPhase { get; private set; }

        protected Transform Player { get; private set; }

        protected Vector2 Origin { get; private set; }

        protected Vector2 Landing { get; private set; }

        /// <summary>存档导出用</summary>
        public Vector2 LandingPosition => Landing;

        /// <summary>读盘重建；直接进等待态</summary>
        public void PlaceAtLanding()
        {
            transform.position = Landing;

            EnterPhase(Phase.Waiting);
        }

        public bool IsCollected => CurrentPhase == Phase.Done;

        public bool IsAlive => CurrentPhase != Phase.Done;

        /// <summary>诊断值；0=Falling 1=Waiting 2=Homing 3=Done</summary>
        public int PhaseCode => (int)CurrentPhase;

        /// <summary>负数或 NaN 视为关闭冷却</summary>
        public void SetPickupCooldown(float duration)
        {
            _pickupCooldown = float.IsNaN(duration) || duration < 0f ? 0f : duration;
        }

        public void Dispose()
        {
            if (this == null) return;

            Destroy(gameObject);
        }

        public void Initialize(in DropSpawnRequest request, DropType type, SeedType seed, DropSpec definition, Transform player)
        {
            Type = type;
            Seed = seed;
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
            if (_pickupCooldown > 0f) _pickupCooldown -= deltaTime;

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

        /// <summary>进入某阶段的一次性动作</summary>
        protected virtual void OnPhaseEntered(Phase phase)
        {
        }

        protected virtual void OnPlayerReached(Transform player)
        {
        }

        /// <remarks>"时长为 0 就直接落地"护栏只写在这里，否则各子类会除出非数坐标</remarks>
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

        /// <remarks>载荷＝什么类别＋几个：订阅方按类型裁决，掉落物不直接改账本</remarks>
        protected void Collect()
        {
            if (CurrentPhase == Phase.Done) return;

            EnterPhase(Phase.Done);

            EventBus<DropCollected>.Publish(new DropCollected(Type, Definition.Amount, Seed));
        }

        // 掉落物是触发器、玩家有刚体 ⇒ Enter 与 Stay 都要听；冷却与"拿不下"都靠这里拦
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

            if (_pickupCooldown > 0f) return;

            PlayerController controller = other.GetComponentInParent<PlayerController>();

            if (controller == null) return;

            if (IsPickupBlocked(controller)) return;

            Player = controller.transform;

            OnPlayerReached(Player);
        }

        /// <summary>领取拦截钩子：默认不拦</summary>
        protected virtual bool IsPickupBlocked(PlayerController controller)
        {
            return false;
        }
    }
}
