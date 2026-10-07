using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>落地冲量的物理帧执行者：渲染帧入队，物理帧出队施加。</summary>
    /// <remarks>
    /// <c>Rigidbody2D.velocity</c> 与 <c>AddForce(Impulse)</c> 只在 <c>FixedUpdate</c> 与下一个 <c>Physics2D.Simulate</c> 之间生效：在渲染帧里写速度会被之后某个大固定步一次性消费，表现是"推得莫名其妙地远"（出队口只有 <see cref="FixedTick"/>，由组合根在物理帧调用）。
    /// 用队列而不是单字段：一个固定步里可能结算多颗球，单字段会静默丢掉其中一颗的冲量。跳过 <see cref="IManagedActor"/>：否则 <c>AddForce</c> 会在账本写出速度之后再写一次 —— 每帧两个速度写者，且完全静默。
    /// </remarks>
    public sealed class ImpulseExecutor
    {
        private readonly struct PendingImpulse
        {
            public readonly Vector2 Point;
            public readonly ThrowTuning Tuning;

            public PendingImpulse(Vector2 point, ThrowTuning tuning)
            {
                Point = point;
                Tuning = tuning;
            }
        }

        // 碰撞体查询缓冲上限；复用同一个数组，避免每次落地都分配。
        private const int OverlapCapacity = 32;

        private readonly Queue<PendingImpulse> _pending = new Queue<PendingImpulse>();
        private readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapCapacity];

        public int PendingCount => _pending.Count;

        public void Enqueue(Vector2 point, ThrowTuning tuning)
        {
            _pending.Enqueue(new PendingImpulse(point, tuning));
        }

        public void FixedTick()
        {
            while (_pending.Count > 0)
            {
                PendingImpulse impulse = _pending.Dequeue();

                PushBodies(impulse.Point, impulse.Tuning);
            }
        }

        public void Clear()
        {
            _pending.Clear();
        }

        /// <remarks>判定按"落点到碰撞体最近点"（<c>ClosestPoint</c>）：大箱子的圆心可能在 2 米外、边缘却贴着落点。查询半径额外放宽 <c>pushQueryMargin</c> 只为别漏候选，判定不看它。</remarks>
        private void PushBodies(Vector2 point, ThrowTuning tuning)
        {
            if (tuning == null) return;

            float radius = tuning.impulseRadius;

            int count = Physics2D.OverlapCircleNonAlloc(
                point,
                radius + Mathf.Max(0f, tuning.pushQueryMargin),
                _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];

                // 从碰撞体往父级找刚体：碰撞体常挂在子节点上，只查自身会漏掉整片角色。
                Rigidbody2D body = hit.GetComponentInParent<Rigidbody2D>();

                if (body == null || body.isKinematic) continue;

                if (hit.GetComponentInParent<IManagedActor>() != null) continue;

                Vector2 nearest = hit.ClosestPoint(point);

                if (Vector2.Distance(point, nearest) > radius) continue;

                Vector2 delta = body.position - point;

                // 正中心命中时方向为零，兜底给"上"：不该让站在落点正中的人免疫击退。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                // 冲量是动量而非速度：按质量折算，让"轻的飞得远、重的推不动"自然成立；质量下限防的是质量趋近 0 时速度趋于无穷。
                float mass = Mathf.Max(body.mass, tuning.impulseMassFloor);

                body.AddForce(direction * (tuning.impulseStrength * mass), ForceMode2D.Impulse);

                // 硬上限：同一个物理步里被多颗球叠推时，速度不该累加到穿模。
                body.velocity = Vector2.ClampMagnitude(body.velocity, tuning.impulseStrength);
            }
        }
    }
}
