using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>水球掉落物，抛物线飞向落点，碰到玩家后跟踪，到手提交</summary>
    public sealed class WaterBallDrop : DropActor
    {
        protected override void BuildBody()
        {
            gameObject.layer = RenderOrder.OverlayLayer;

            var renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                renderer.sortingOrder = RenderOrder.BallOrder(Landing.y);
            }

            var collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
                if (Definition != null && Definition.TriggerRadius > 0f)
                {
                    collider.radius = Definition.TriggerRadius;
                }
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

            Vector2 current = transform.position;
            var target = new Vector2(Player.position.x, Player.position.y);

            Vector2 next = Vector2.MoveTowards(current, target, Definition.HomingSpeed * deltaTime);

            transform.position = next;

            if (Vector2.Distance(next, target) > Definition.ReachDistance) return;

            Collect();
        }

        protected override void OnPlayerReached(Transform player)
        {
            EnterPhase(Phase.Homing);
        }
    }
}
