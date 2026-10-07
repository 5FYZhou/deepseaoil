using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Presentation.Primitive;
using DeepseaOil.Presentation.Visual;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>水球掉落物：抛物线飞向落点 → 等玩家碰到 → 飞向玩家 → 发一条领取事实。</summary>
    public sealed class WaterBallDrop : DropActor
    {
        /// <inheritdoc />
        protected override void BuildBody()
        {
            gameObject.layer = RenderOrder.OverlayLayer;

            var renderer = gameObject.AddComponent<SpriteRenderer>();

            // Y-Sort 档位按**落点**的 y 取一次：飞行途中改档会让它在半空里穿来穿去，而落点才是它最终待的地方。
            PrimitiveSprites.Configure(
                renderer,
                PrimitiveSprites.Circle,
                Definition.Color,
                RenderOrder.BallOrder(Landing.y),
                Definition.BodyDiameter);

            // 触发体只挂在一边即可（玩家侧有刚体）：掉落物自己不需要 Rigidbody2D。
            var collider = gameObject.AddComponent<CircleCollider2D>();

            collider.isTrigger = true;
            collider.radius = Definition.TriggerRadius;
        }

        /// <inheritdoc />
        protected override void TickFalling(float deltaTime)
        {
            float t = AdvanceFalling(deltaTime);

            Vector2 position = Vector2.Lerp(Origin, Landing, t);

            // 弧高 0 → 1 → 0：两端恰好为 0，落地那一刻高度精确归零。
            // 必须复用 Ballistics.ArcHeight01（球的飞行用同一个式子）：两份实现漂了会出"陷进地面"这类只有肉眼能发现的偏差。
            position.y += Definition.ArcHeight * Ballistics.ArcHeight01(t);

            transform.position = position;

            if (t < 1f) return;

            transform.position = Landing;

            EnterPhase(Phase.Waiting);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        protected override void OnPlayerReached(Transform player)
        {
            EnterPhase(Phase.Homing);
        }
    }
}
