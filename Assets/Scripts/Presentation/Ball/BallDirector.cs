using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Projectile;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Ball
{
    /// <summary>球的调度器，由组合根显式造与驱动</summary>
    /// <remarks>飞行与改格都在渲染帧，只有冲量跨物理帧。</remarks>
    public sealed class BallDirector
    {
        private readonly List<BallActor> _flying = new List<BallActor>();

        private readonly Dictionary<BallType, ProjectileSpec> _balls = new Dictionary<BallType, ProjectileSpec>();

        private GridLogic _grid;
        private ImpulseExecutor _impulses;
        private Transform _ballRoot;
        private System.Action<AudioId> _requestSfx;

        // 球表只有水/土，种子弹借水球弹道作外壳
        private const BallType SeedShellBall = BallType.Water;

        private Action<SeedType, Vector2> _seedLanded;

        public int FlyingCount => _flying.Count;

        public bool IsReady => _grid != null;

        public void Attach(
            GridLogic grid,
            IReadOnlyList<ProjectileSpec> balls,
            ImpulseExecutor impulses,
            Transform ballRoot,
            System.Action<AudioId> requestSfx)
        {
            _grid = grid;
            _impulses = impulses;
            _ballRoot = ballRoot;
            _requestSfx = requestSfx;

            _balls.Clear();

            if (balls == null) return;

            for (int i = 0; i < balls.Count; i++)
            {
                ProjectileSpec ball = balls[i];

                _balls[ball.Type] = ball;
            }
        }

        /// <summary>注入种子弹落地处理</summary>
        public void SetSeedLandingHandler(Action<SeedType, Vector2> handler)
        {
            _seedLanded = handler;
        }

        public void Tick(float deltaTime)
        {
            // 倒序：正序删除会跳过下一个元素
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

        /// <summary>按已裁决通过的意图投一颗球</summary>
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

        /// <summary>投一颗带种子载荷的球；planted 才是已发射</summary>
        /// <remarks>校验必须全在这里做完：次数已在上游扣掉，构造后才发现非法就只剩一个拆不掉的球。</remarks>
        public bool ThrowSeed(Vector2 origin, Vector2 target, SeedType seed, out bool planted)
        {
            planted = false;

            if (seed == SeedType.None) return false;

            if (float.IsNaN(origin.x) || float.IsNaN(origin.y)
                || float.IsNaN(target.x) || float.IsNaN(target.y))
            {
                return false;
            }

            if (!_balls.TryGetValue(SeedShellBall, out ProjectileSpec shell))
            {
                Debug.LogError($"[Ball] 球表里没有 {SeedShellBall}，挑不出种子弹壳，这次种子投掷被丢弃。");
                return false;
            }

            float distance = Vector2.Distance(origin, target);

            var data = new ProjectileTrajectory(shell.Type, origin, target, distance, shell);

            _flying.Add(new BallActor(in data, shell, _ballRoot, OnLanded, seed));

            planted = true;

            return true;
        }

        /// <summary>清空在飞球与待施加冲量</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _flying.Count; i++)
            {
                _flying[i]?.Dispose();
            }

            _flying.Clear();

            _impulses?.Clear();
        }

        /// <remarks>球种先改格再排冲量；种子弹只调播种回调，不进 OnBallHit。</remarks>
        private void OnLanded(BallActor ball, Vector2 point)
        {
            _requestSfx?.Invoke(AudioId.PlayerThrowLand);

            if (ball.Seed != SeedType.None)
            {
                SeedType landed = ball.Seed;

                // 先清载荷再回调，防重入时本球被二次当种子弹
                ball.Seed = SeedType.None;

                _seedLanded?.Invoke(landed, point);

                return;
            }

            if (_grid != null && _grid.Geometry.IsValid)
            {
                _grid.OnBallHit(_grid.WorldToCell(point), ball.Definition.Type);
            }

            _impulses?.Enqueue(point, ball.Definition.Tuning);
        }
    }
}
