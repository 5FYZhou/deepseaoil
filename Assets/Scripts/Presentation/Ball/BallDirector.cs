using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Projectile;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>球的调度器：<b>造 ＋ 持 ＋ 驱</b>。玩家抛出去的每一颗球都归它。</summary>
    /// <remarks>
    /// 帧相位：飞行与改格都在渲染帧；只有冲量跨物理帧（<see cref="ImpulseExecutor"/> 的短队列）—— 本类不碰物理。它不是 MonoBehaviour，由组合根显式造、显式驱动。
    /// </remarks>
    public sealed class BallDirector : IBallLogicEffectContext
    {
        private readonly List<BallActor> _flying = new List<BallActor>();

        private readonly Dictionary<BallType, ProjectileSpec> _balls = new Dictionary<BallType, ProjectileSpec>();

        private readonly Dictionary<BallType, IBallLogicEffect> _effects = new Dictionary<BallType, IBallLogicEffect>();

        private GridLogic _grid;
        private ImpulseExecutor _impulses;
        private Transform _ballRoot;

        public int FlyingCount => _flying.Count;

        /// <summary>装配是否完成；没装配时一切操作是 no-op（不报错）。</summary>
        public bool IsReady => _grid != null;

        /// <summary>装配（依赖全部由参数给出，本类没有 inspector 字段）；<c>ballRoot</c> 为 <c>null</c> 时球建在场景根下。</summary>
        public void Attach(
            GridLogic grid,
            IReadOnlyList<ProjectileSpec> balls,
            ImpulseExecutor impulses,
            Transform ballRoot)
        {
            _grid = grid;
            _impulses = impulses;
            _ballRoot = ballRoot;

            _balls.Clear();
            _effects.Clear();

            if (balls == null) return;

            for (int i = 0; i < balls.Count; i++)
            {
                ProjectileSpec ball = balls[i];

                _balls[ball.Type] = ball;

                // 每个球种都走同一条落地链（元素反应）：旧版"按 projectile.tile_state 决定改不改格"已随上游表改版作废 ——
                // 那一列不存在了，而"这颗球落地之后世界变成什么"改由 element_rule 算出来。
                _effects[ball.Type] = new TileStateLogicEffect();
            }
        }

        /// <summary>推进一个渲染帧：驱动在飞的球，落地那一帧完成"改格 ＋ 入队冲量"再回收该球；<c>deltaTime</c> 为 0（暂停）时球自然冻结。</summary>
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

        /// <summary>按已经裁决通过的意图真的投一颗球。</summary>
        /// <returns>球种没定义（表里少一行）时为 <c>false</c>。</returns>
        /// <remarks>调用方是 <c>CombatRoot.RequestThrow</c>；走到这里时"落点合法"已经问过了。距离与落点取自同一份意图 —— 两处各算一次会漂。</remarks>
        public bool Throw(in ThrowIntent intent)
        {
            if (!_balls.TryGetValue(intent.Ball, out ProjectileSpec definition))
            {
                Debug.LogError($"[Ball] projectile 表里没有球种 {intent.Ball}，这次投掷被丢弃。");
                return false;
            }

            float distance = Vector2.Distance(intent.Origin, intent.Target);

            var data = new ProjectileTrajectory(intent.Ball, intent.Origin, intent.Target, distance, definition);

            _flying.Add(new BallActor(in data, definition, _ballRoot, OnLanded));

            return true;
        }

        /// <summary>清掉在飞的球与待施加的冲量（打空重来 / 切场景）；冲量也要清，否则那一下会砸在下一局的箱子上。</summary>
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
        /// <remarks>球效果唯一被允许的世界操作：把"落点格 ＋ 球元素"交给世界侧结算元素反应（新状态与效果清单都由格子自己按配置算）。</remarks>
        public void RequestTileState(Vector3Int cell, in ElementValue element)
        {
            if (_grid == null) return;

            _grid.OnBallHit(cell, in element);
        }

        /// <summary>一次落地的完整结算：先改世界状态，再排冲量。</summary>
        /// <remarks>顺序即语义：改格会在同一帧内结算格上目标的伤害（可能把敌人打死并注销自己）；冲量只进队列，下一个物理帧才生效。</remarks>
        private void OnLanded(BallActor ball, Vector2 point)
        {
            if (_grid != null && _grid.Geometry.IsValid)
            {
                Vector3Int cell = _grid.WorldToCell(point);

                if (_effects.TryGetValue(ball.Type, out IBallLogicEffect effect) && effect != null)
                {
                    ProjectileSpec definition = ball.Definition;

                    effect.Apply(cell, definition, this);
                }
            }

            _impulses?.Enqueue(point, ball.Definition.Tuning);
        }
    }
}
