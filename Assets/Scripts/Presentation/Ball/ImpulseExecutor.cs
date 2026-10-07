using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Movement;
using UnityEngine;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>
    /// 落地冲量的<b>物理帧执行者</b>：渲染帧入队，物理帧出队施加。
    /// </summary>
    /// <remarks>
    /// <b>为什么必须跨相位：</b><c>Rigidbody2D.velocity</c> 与 <c>AddForce(Impulse)</c> 只在
    /// <c>FixedUpdate</c> 与下一个 <c>Physics2D.Simulate</c> 之间生效。在渲染帧里写速度会在
    /// 多个渲染帧之后的一个大固定步里被一次性消费，表现是"推得莫名其妙地远"。
    /// <para><b>为什么是队列而不是一个字段：</b>一个固定步里可能结算多颗球，用单字段会静默丢掉
    /// 其中一颗的冲量。</para>
    /// <para><b>跳过自持速度账本的角色（<see cref="IManagedActor"/>）：</b>它们的速度由各自的账本写。
    /// 不跳的话，<c>AddForce</c> 会在账本写出速度之后<b>又写一次</b>速度 —— 每帧两个速度写者，
    /// 正是白模阶段还掉的那笔技术债。<b>名单由标记接口回答</b>，不是硬编码类型 ——
    /// 将来加一个自带账本的角色时，漏改的表现本来是"被推了两次速度"且完全静默。</para>
    /// <para><b>冲量参数取"哪一份调参"由入队方给出</b>（球的取值边界自持自己的调参）：
    /// 于是"哪种球推得更狠"是球的事，不是这个执行者的事。</para>
    /// </remarks>
    public sealed class ImpulseExecutor
    {
        /// <summary>一次待结算的落地冲量。</summary>
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

        /// <summary>冲量查询的碰撞体缓冲上限；复用同一个数组，避免每次落地都分配。</summary>
        private const int OverlapCapacity = 32;

        private readonly Queue<PendingImpulse> _pending = new Queue<PendingImpulse>();
        private readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapCapacity];

        /// <summary>待施加的冲量条数（诊断 / 测试读数）。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>入队一次落地（由球在渲染帧调用）。</summary>
        /// <param name="point">落点（贴地世界坐标）。</param>
        /// <param name="tuning">这颗球自己的冲量调参；为 <c>null</c> 时本次不推。</param>
        public void Enqueue(Vector2 point, ThrowTuning tuning)
        {
            _pending.Enqueue(new PendingImpulse(point, tuning));
        }

        /// <summary>出队并施加全部待结算冲量（由组合根在物理帧调用）。</summary>
        public void FixedTick()
        {
            while (_pending.Count > 0)
            {
                PendingImpulse impulse = _pending.Dequeue();

                PushBodies(impulse.Point, impulse.Tuning);
            }
        }

        /// <summary>丢掉全部待结算冲量（清场 / 切场景）。</summary>
        public void Clear()
        {
            _pending.Clear();
        }

        /// <summary>
        /// 给半径内的<b>无生命</b>刚体一次冲量。
        /// </summary>
        /// <remarks>
        /// <b>判定按"落点到碰撞体最近点的距离"</b>：用 <c>Collider2D.ClosestPoint</c> 而不是
        /// 刚体圆心 —— 一个大箱子的圆心可能在 2 米外、边缘却贴着落点，按圆心判会把它漏掉。
        /// 查询半径额外放宽 <c>pushQueryMargin</c> 只为"别漏候选"，判定不看它。
        /// </remarks>
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

                // 自持速度账本的角色不归物理引擎推（见类注释）。
                if (hit.GetComponentInParent<IManagedActor>() != null) continue;

                // 精确判定：取碰撞体上离落点最近的点，它到落点的距离必须落在作用半径内。
                Vector2 nearest = hit.ClosestPoint(point);

                if (Vector2.Distance(point, nearest) > radius) continue;

                Vector2 delta = body.position - point;

                // 正中心命中时方向为零，兜底给"上"：不该让站在落点正中的人免疫击退。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                // 冲量是动量而非速度：按质量折算，让"轻的飞得远、重的推不动"自然成立。
                // 质量下限防的是"质量趋近 0 时速度趋于无穷"。
                float mass = Mathf.Max(body.mass, tuning.impulseMassFloor);

                body.AddForce(direction * (tuning.impulseStrength * mass), ForceMode2D.Impulse);

                // 硬上限：同一个物理步里被多颗球叠推时，速度不该累加到穿模。
                body.velocity = Vector2.ClampMagnitude(body.velocity, tuning.impulseStrength);
            }
        }
    }
}
