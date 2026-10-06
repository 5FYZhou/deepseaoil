using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Projectile;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>
    /// 球的调度器：<b>造 ＋ 持 ＋ 驱</b>。玩家抛出去的每一颗球都归它。
    /// </summary>
    /// <remarks>
    /// <b>它是"球"这条链上唯一的管理者</b>：生成（<see cref="Throw"/>）、逐帧推进（<see cref="Tick"/>）、
    /// 落地结算（改格 ＋ 入队冲量）、清场（<see cref="ClearAll"/>）。收口前这四件事散在
    /// <c>ThrowController</c>（建球与飞行）与 <c>LandingResolver</c>（改格 ＋ 冲量 ＋ 播环）里，
    /// "球的落地有三个管理者"。
    /// <para><b>帧相位：</b>飞行与"改格"都在<b>渲染帧</b>（非物理逻辑）；只有冲量要跨到物理帧，
    /// 交给 <see cref="ImpulseExecutor"/> 的短队列 —— 本类不碰物理。</para>
    /// <para><b>它不是 MonoBehaviour</b>：没有生命周期需求，由组合根显式造、显式驱动。
    /// 于是"谁在什么时候驱动球"只有一个答案。</para>
    /// <para><b>落地语义：</b>球的逻辑效果由球定义决定（<see cref="BallDefinition.HasLandingEffect"/>），
    /// 表现侧的冲量则每颗球都做 —— "改世界状态"与"推无生命刚体"是两件事。</para>
    /// </remarks>
    public sealed class BallDirector : IBallLogicEffectContext
    {
        /// <summary>在飞的球（落地即回收，这里只做推进与清理）。</summary>
        private readonly List<BallActor> _flying = new List<BallActor>();

        /// <summary>球种 → 定义。</summary>
        private readonly Dictionary<BallType, BallDefinition> _balls = new Dictionary<BallType, BallDefinition>();

        /// <summary>球种 → 落地逻辑效果（"什么都不做"也有一个具名实现）。</summary>
        private readonly Dictionary<BallType, IBallLogicEffect> _effects = new Dictionary<BallType, IBallLogicEffect>();

        private GridLogic _grid;
        private ThrowTuning _tuning;
        private ImpulseExecutor _impulses;
        private Transform _ballRoot;

        /// <summary>在飞的球数（诊断 / 测试读数）。</summary>
        public int FlyingCount => _flying.Count;

        /// <summary>装配是否完成（没装配时一切操作是 no-op）。</summary>
        public bool IsReady => _grid != null;

        /// <summary>
        /// 装配。<b>依赖全部由参数给出</b>（本类没有 inspector 字段），所以"忘了接线"这种失败模式不存在。
        /// </summary>
        /// <param name="grid">格子门面（落点 → 格）。</param>
        /// <param name="balls">全部球定义（取值边界）。</param>
        /// <param name="tuning">观感与冲量调参（球的实体默认吃这一份）。</param>
        /// <param name="impulses">冲量的物理帧执行者。</param>
        /// <param name="ballRoot">球的父物体；为 <c>null</c> 时建在场景根下。</param>
        public void Attach(
            GridLogic grid,
            IReadOnlyList<BallDefinition> balls,
            ThrowTuning tuning,
            ImpulseExecutor impulses,
            Transform ballRoot)
        {
            _grid = grid;
            _tuning = tuning;
            _impulses = impulses;
            _ballRoot = ballRoot;

            _balls.Clear();
            _effects.Clear();

            if (balls == null) return;

            for (int i = 0; i < balls.Count; i++)
            {
                BallDefinition ball = balls[i];

                _balls[ball.Type] = ball;

                // 落地后"改格"还是"什么都不做"由表里的 tile_state 决定：
                // Normal = 不改 ⇒ 这一球没有世界效果。于是"土球现在没有效果"是一条配置事实，
                // 而不是代码里的一个 if。
                _effects[ball.Type] = ball.HasLandingEffect
                    ? (IBallLogicEffect)new TileStateLogicEffect()
                    : new NullLogicEffect();
            }
        }

        /// <summary>
        /// 推进一个渲染帧：驱动在飞的球；<b>落地那一帧就完成"改格 ＋ 入队冲量"</b>，随后回收该球。
        /// </summary>
        /// <param name="deltaTime">本帧时长（<c>Time.deltaTime</c>）；暂停时为 0，球自然冻结。</param>
        public void Tick(float deltaTime)
        {
            // 倒序：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                BallActor ball = _flying[i];

                if (ball == null)
                {
                    _flying.RemoveAt(i);
                    continue;
                }

                ball.Tick(deltaTime);

                if (ball.IsAlive) continue;

                ball.Dispose();
                _flying.RemoveAt(i);
            }
        }

        /// <summary>
        /// 按已经裁决通过的意图真的投一颗球。
        /// </summary>
        /// <param name="intent">意图（球种 ＋ 目标格 ＋ 出手点与落点）。</param>
        /// <returns>球种没有定义（表里少一行）时为 <c>false</c>。</returns>
        /// <remarks>调用方是 <c>CombatRoot.RequestThrow</c> —— 走到这里时"落点合法"已经问过了，
        /// 本类不重复判断。距离与落点取自<b>同一份</b>意图：两处各算一次必然会漂。</remarks>
        public bool Throw(in ThrowIntent intent)
        {
            if (!_balls.TryGetValue(intent.Ball, out BallDefinition definition))
            {
                Debug.LogError($"[Ball] projectile 表里没有球种 {intent.Ball}，这次投掷被丢弃。");
                return false;
            }

            float distance = Vector2.Distance(intent.Origin, intent.Target);

            var data = new BallData(intent.Ball, intent.Origin, intent.Target, distance, in definition.Throw);

            _flying.Add(new BallActor(in data, in definition, _tuning, _ballRoot, OnLanded));

            return true;
        }

        /// <summary>清掉在飞的球与待施加的冲量（打空重来 / 切场景）。</summary>
        /// <remarks><b>冲量也要清：</b>清场那一帧的球还没落地，但它的冲量已经排进队列 ——
        /// 不清的话那一下会砸在下一局的箱子上（球没了，箱子飞了）。</remarks>
        public void ClearAll()
        {
            for (int i = 0; i < _flying.Count; i++)
            {
                _flying[i]?.Dispose();
            }

            _flying.Clear();

            _impulses?.Clear();
        }

        /// <inheritdoc />
        /// <remarks>球效果唯一被允许的世界操作：把格子切到该状态（冲击由格子自己按配置结算）。</remarks>
        public void RequestTileState(Vector3Int cell, TileStateType next)
        {
            if (_grid == null) return;

            _grid.OnBallHit(cell, next);
        }

        /// <summary>
        /// 一次落地的完整结算：<b>先改世界状态，再排冲量</b>。
        /// </summary>
        /// <remarks>
        /// 顺序即语义：改格会在同一帧内结算格上目标的伤害（可能把敌人打死并注销自己），
        /// 而冲量只进队列、下一个物理帧才生效 —— 于是"这一格发生了什么"先成立，
        /// "无生命刚体被推到哪"随后发生，两者不会互相看见半成品状态。
        /// </remarks>
        private void OnLanded(BallActor ball, Vector2 point)
        {
            if (_grid != null && _grid.Geometry.IsValid)
            {
                Vector3Int cell = _grid.WorldToCell(point);

                if (_effects.TryGetValue(ball.Type, out IBallLogicEffect effect) && effect != null)
                {
                    // 局部复制才能按 in 传递（属性不能直接按 in 传参）。
                    BallDefinition definition = ball.Definition;

                    effect.Apply(cell, in definition, this);
                }
            }

            _impulses?.Enqueue(point, ball.Tuning);
        }
    }
}
