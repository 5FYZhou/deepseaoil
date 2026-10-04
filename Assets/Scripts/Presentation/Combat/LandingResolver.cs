using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Projectile;
using DeepseaOil.Presentation.Actor;
using DeepseaOil.Presentation.Effects;
using DeepseaOil.Presentation.Grid;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Combat
{
    /// <summary>
    /// 球落地结算：通知格子、给无生命刚体冲量、画落地环。<b>它是 <see cref="IBallEffectContext"/> 的实现</b>。
    /// </summary>
    /// <remarks>
    /// <b>球不再直接伤害敌人</b>：伤害全部由格子产生（见 <c>GridLogic</c>）。
    /// 于是落地这一帧只剩三件事：世界状态怎么变（球效果）、无生命刚体怎么动（物理冲量）、
    /// 玩家看到什么（落地环）。
    /// <para><b>冲量必须在物理帧施加，不能在渲染帧：</b><c>Rigidbody2D.velocity</c> 与
    /// <c>AddForce(Impulse)</c> 只在 <c>FixedUpdate</c> 与下一个 <c>Physics2D.Simulate</c> 之间生效；
    /// 在 <c>Update</c> 里写速度会在多个渲染帧后的大固定步里被一次性消费，表现是"推得莫名其妙地远"。
    /// 所以球落地（渲染帧）只<b>入队</b>，<see cref="FixedTick"/> 才出队结算。</para>
    /// <para><b>队列而不是单个字段：</b>一个固定步里可能结算多颗球，用单字段会静默丢掉一颗球的冲量。</para>
    /// </remarks>
    public sealed class LandingResolver : MonoBehaviour, IBallEffectContext
    {
        /// <summary>一次待结算的落地。</summary>
        private readonly struct PendingLanding
        {
            public readonly Vector2 Point;
            public readonly BallSpec Ball;

            public PendingLanding(Vector2 point, in BallSpec ball)
            {
                Point = point;
                Ball = ball;
            }
        }

        private readonly Queue<PendingLanding> _pending = new Queue<PendingLanding>();
        private readonly Dictionary<BallType, IBallEffect> _effects = new Dictionary<BallType, IBallEffect>();

        /// <summary>冲量查询的碰撞体缓冲（复用，避免每次落地都分配一个数组）。</summary>
        private readonly Collider2D[] _overlapBuffer = new Collider2D[32];

        private GridLogic _grid;
        private ThrowTuning _tuning;

        /// <summary>
        /// 装配。
        /// </summary>
        /// <param name="grid">格子门面。</param>
        /// <param name="tuning">观感与经济调参（冲量半径 / 强度 / 落地环时长）。</param>
        /// <param name="balls">全部球种配置（决定每颗球落地后发生什么）。</param>
        public void Initialize(GridLogic grid, ThrowTuning tuning, IReadOnlyList<BallSpec> balls)
        {
            _grid = grid;
            _tuning = tuning;

            BuildEffects(balls);
        }

        /// <summary>入队一次落地（由球驱动器在渲染帧调用）。</summary>
        public void Enqueue(Vector2 point, BallType type, in BallSpec ball)
        {
            _pending.Enqueue(new PendingLanding(point, in ball));
        }

        /// <summary>出队结算（由组合根在物理帧调用）。</summary>
        public void FixedTick()
        {
            while (_pending.Count > 0)
            {
                PendingLanding landing = _pending.Dequeue();

                Resolve(landing.Point, in landing.Ball);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 这是球效果唯一被允许的世界操作：把格子切到该球种配置的状态，并结算这次落地的冲击。
        /// </remarks>
        public void RequestTileState(Vector3Int cell, in BallSpec ball)
        {
            if (_grid == null) return;

            _grid.OnBallHit(cell, in ball);
        }

        /// <summary>清空待结算队列（暂停 / 切场景）。</summary>
        public void Clear()
        {
            _pending.Clear();
        }

        private void BuildEffects(IReadOnlyList<BallSpec> balls)
        {
            _effects.Clear();

            if (balls != null)
            {
                for (int i = 0; i < balls.Count; i++)
                {
                    BallSpec ball = balls[i];

                    // 落地后"改格子"还是"什么都不做"，由表里的 tile_state 决定：
                    // Normal = 不改 ⇒ 这一球没有世界效果。于是"土球现在没有效果"是一条配置事实，
                    // 而不是代码里的一个 if。
                    _effects[ball.Type] = ball.TileState == TileStateType.Normal
                        ? (IBallEffect)new NullBallEffect()
                        : new TileStateBallEffect();
                }
            }
        }

        /// <summary>结算一次落地。</summary>
        private void Resolve(Vector2 point, in BallSpec ball)
        {
            if (_grid == null || !_grid.Geometry.IsValid) return;

            Vector3Int cell = _grid.WorldToCell(point);

            TileStateType previousState = _grid.StateOf(cell);

            if (_effects.TryGetValue(ball.Type, out IBallEffect effect) && effect != null)
            {
                effect.Apply(cell, in ball, this);
            }

            bool stateChanged = _grid.StateOf(cell) != previousState;

            PushBodies(point);

            PlayRings(point, cell, in ball, stateChanged);
        }

        /// <summary>
        /// 给半径内的<b>无生命</b>刚体一次冲量。
        /// </summary>
        /// <remarks>
        /// <b>跳过角色：</b>它们的速度由自己的账本写（敌人 <c>EnemyLogic</c>、玩家 <c>PlayerLogic</c>）。
        /// 不跳的话，<c>Rigidbody2D.AddForce</c> 会在账本写出速度之后<b>又写一次</b>速度 ——
        /// 每帧两个速度写者，正是白模阶段还掉的那笔技术债。
        /// <para><b>判定按"落点到碰撞体最近点的距离"</b>：用 <c>Collider2D.ClosestPoint</c> 而不是
        /// 刚体圆心 —— 一个大箱子的圆心可能在 2 米外、边缘却贴着落点，按圆心判会把它漏掉。
        /// 查询半径额外放宽 <c>pushQueryMargin</c> 只为"别漏候选"，判定不看它。</para>
        /// </remarks>
        private void PushBodies(Vector2 point)
        {
            if (_tuning == null) return;

            float radius = _tuning.impulseRadius;

            int count = Physics2D.OverlapCircleNonAlloc(
                point,
                radius + Mathf.Max(0f, _tuning.pushQueryMargin),
                _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlapBuffer[i];

                // 从碰撞体往父级找刚体：碰撞体常挂在子节点上，只查自身会漏掉整片角色。
                Rigidbody2D body = hit.GetComponentInParent<Rigidbody2D>();

                if (body == null || body.isKinematic) continue;

                // 角色有自己的速度账本，这不归物理引擎管。
                if (hit.GetComponentInParent<EnemyActor>() != null) continue;
                if (hit.GetComponentInParent<PlayerController>() != null) continue;

                // 精确判定：取碰撞体上离落点最近的点，它到落点的距离必须落在作用半径内。
                Vector2 nearest = hit.ClosestPoint(point);

                if (Vector2.Distance(point, nearest) > radius) continue;

                Vector2 delta = body.position - point;

                // 正中心命中时方向为零，兜底给"上"：不该让站在落点正中的人免疫击退。
                Vector2 direction = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.up;

                // 冲量是动量而非速度：按质量折算，让"轻的飞得远、重的推不动"自然成立。
                // 质量下限防的是"质量趋近 0 时速度趋于无穷"。
                float mass = Mathf.Max(body.mass, _tuning.impulseMassFloor);

                body.AddForce(direction * (_tuning.impulseStrength * mass), ForceMode2D.Impulse);

                // 硬上限：同一个物理步里被多颗球叠推时，速度不该累加到穿模。
                body.velocity = Vector2.ClampMagnitude(body.velocity, _tuning.impulseStrength);
            }
        }

        /// <summary>
        /// 画落地环：<b>每画一个圈都精确等于一件事的生效范围</b>。
        /// </summary>
        /// <param name="point">落点。</param>
        /// <param name="cell">落点格。</param>
        /// <param name="ball">球种配置。</param>
        /// <param name="stateChanged">这一球是否真的改变了该格的状态。</param>
        /// <remarks>
        /// 两个圈（半径与颜色都不同，所以不会糊成一个粗环）：
        /// <list type="number">
        /// <item><b>冲量圈</b>：半径 = <c>tuning.impulseRadius</c>，颜色 = 球种色 ——
        /// "这一下把无生命刚体推到了哪"。</item>
        /// <item><b>格子圈</b>：半径 = 半格，颜色 = 灰白 —— "哪一格的状态变了"。
        /// <b>只有真的变了才画</b>：没变还画就是在撒谎。</item>
        /// </list>
        /// <para>白模那两个圈是"晚 0.05 秒出现的同心环"（内圈击退、外圈泥浆范围）。
        /// 迁移后泥浆变成<b>整格</b>而不是一个半径，外圈因此换成"格子圈"，
        /// 而顺序错开在"两个圈语义不同"之后不再必要 —— 半径差一个数量级、颜色也不同，读得出来。</para>
        /// </remarks>
        private void PlayRings(Vector2 point, Vector3Int cell, in BallSpec ball, bool stateChanged)
        {
            float duration = _tuning != null ? _tuning.landingRingDuration : 0.15f;

            // ① 冲量圈
            EffectContext impulseCtx = EffectContext.At(point);
            impulseCtx.Tint = CombatPalette.BallColor(ball.Type);
            impulseCtx.Radius = _tuning != null ? _tuning.impulseRadius : 1.2f;
            impulseCtx.Duration = duration;

            EffectModule.Play(EffectId.LandingRing, in impulseCtx);

            if (!stateChanged) return;

            // ② 格子圈
            EffectContext cellCtx = EffectContext.At(_grid.Geometry.CellCenter(cell));
            cellCtx.Tint = CombatPalette.TileEffect;
            cellCtx.Radius = _grid.Geometry.CellSize * 0.5f;
            cellCtx.Duration = duration;

            EffectModule.Play(EffectId.LandingRing, in cellCtx);
        }
    }
}
