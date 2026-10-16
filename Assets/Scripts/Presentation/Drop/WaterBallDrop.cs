using DeepseaOil.Foundation;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>水球掉落物，抛物线飞向落点，碰到玩家后跟踪，到手提交</summary>
    public sealed class WaterBallDrop : DropActor
    {
        private float _homingDuration;

        protected override void BuildBody()
        {
            gameObject.layer = RenderOrder.OverlayLayer;

            var renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                renderer.sortingLayerName = SortingLayerConsts.Actor;
                renderer.sortingOrder = RenderOrder.BallOrder(Landing.y);
            }

            var collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        protected override void OnPhaseEntered(Phase phase)
        {
            base.OnPhaseEntered(phase);

            if (phase == Phase.Homing)
            {
                _homingDuration = 0f;
            }
        }

        protected override void TickFalling(float deltaTime)
        {
            float t = AdvanceFalling(deltaTime);

            Vector2 position = Vector2.Lerp(Origin, Landing, t);

            // 必须复用 Ballistics.ArcHeight01（与球同式），弧高 0→1→0，漂了会"陷进地面"
            position.y += Definition.ArcHeight * Ballistics.ArcHeight01(t);

            transform.position = position;

            if (t < 1f) return;

            transform.position = Landing;

            EnterPhase(Phase.Waiting);
        }

        protected override void TickHoming(float deltaTime)
        {
            if (Player == null)
            {
                EnterPhase(Phase.Waiting);
                return;
            }

            _homingDuration += deltaTime;

            Vector2 current = transform.position;
            Vector2 target = Player.position;
            Vector2 toTarget = target - current;
            float distance = toTarget.magnitude;

            if (distance <= Definition.ReachDistance || distance <= 0.15f)
            {
                Collect();
                return;
            }

            float speed = Definition.HomingSpeed + (30f * _homingDuration * _homingDuration);
            Vector2 next = Vector2.MoveTowards(current, target, speed * deltaTime);

            transform.position = next;
        }

        protected override void OnPlayerReached(Transform player)
        {
            EnterPhase(Phase.Homing);
        }
    }
}
