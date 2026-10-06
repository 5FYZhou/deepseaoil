using DeepseaOil.Data;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>
    /// 水球掉落物（原 <c>WaterBall</c>）：抛物线飞向落点 → 等玩家碰到 → 飞向玩家 → 发一条领取事实。
    /// </summary>
    /// <remarks>
    /// 数值全部来自取值定义（<see cref="DropDefinition"/>）；本类只管<b>行为与观感</b>：
    /// 怎么飞、长什么样、碰撞体多大。
    /// </remarks>
    public sealed class WaterBallDrop : DropActor
    {
        /// <summary>视觉直径（世界单位）。白模件：正式美术接入时随图元一起换掉。</summary>
        private const float BodyDiameter = 0.3f;

        /// <summary>触发半径（世界单位）。只回答"碰没碰到玩家"，不参与任何判定。</summary>
        private const float TriggerRadius = 0.15f;

        /// <inheritdoc />
        protected override void BuildBody()
        {
            gameObject.layer = RenderOrder.OverlayLayer;

            var renderer = gameObject.AddComponent<SpriteRenderer>();

            // 档位按**落点**的 y 取一次：掉落物参与 Y-Sort（与球同一频带），而飞行途中改档
            // 会让它在半空里穿来穿去 —— 落点就是它最终待在的地方。
            PrimitiveSprites.Configure(
                renderer,
                PrimitiveSprites.Circle,
                CombatPalette.WaterBall,
                RenderOrder.BallOrder(Landing.y),
                BodyDiameter);

            // 触发体只需要挂在一边（玩家有刚体），所以掉落物自己不需要 Rigidbody2D。
            var collider = gameObject.AddComponent<CircleCollider2D>();

            collider.isTrigger = true;
            collider.radius = TriggerRadius;
        }

        /// <inheritdoc />
        protected override void TickFalling(float deltaTime)
        {
            float t = AdvanceFalling(deltaTime);

            Vector2 position = Vector2.Lerp(Origin, Landing, t);

            // 0 → 1 → 0：两端恰好为 0，所以"落地"那一刻高度精确归零。
            // （同一个式子也写在 BallData.SampleHeight01 里；"数学果实收进地基"是后面批次的事。）
            position.y += 4f * Definition.ArcHeight * t * (1f - t);

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
                // 玩家不见了（切场景 / 被销毁）：回到"等触发"状态，不销毁自己。
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
