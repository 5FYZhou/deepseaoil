using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>落地冲量的物理帧执行者，渲染帧入队、物理帧出队</summary>
    /// <remarks>velocity/AddForce 只在 FixedUpdate 与下一次 Simulate 间生效，渲染帧写速度会被大固定步一次性消费</remarks>
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

        // 复用数组避免落地分配
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

        /// <remarks>判定按落点到碰撞体最近点 ClosestPoint，大箱子圆心可能在 2 米外、边缘却贴着落点；pushQueryMargin 只放宽查询半径，判定不看它</remarks>
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

                // 从碰撞体往父级找刚体：只查自身会漏掉整片角色
                Rigidbody2D body = hit.GetComponentInParent<Rigidbody2D>();

                if (body == null || body.isKinematic) continue;

                if (hit.GetComponentInParent<IManagedActor>() != null) continue;

                Vector2 nearest = hit.ClosestPoint(point);

                if (Vector2.Distance(point, nearest) > radius) continue;

                Vector2 delta = body.position - point;

                // 正中心命中方向为零，兜底给上
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                // 冲量是动量而非速度，按质量折算；质量下限防质量趋 0 时速度无穷
                float mass = Mathf.Max(body.mass, tuning.impulseMassFloor);

                body.AddForce(direction * (tuning.impulseStrength * mass), ForceMode2D.Impulse);

                // 硬上限：同一物理步被多颗球叠推时不穿模
                body.velocity = Vector2.ClampMagnitude(body.velocity, tuning.impulseStrength);
            }
        }
    }
}
