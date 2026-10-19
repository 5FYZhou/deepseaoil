using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人转向器：流场跟随 ＋ 入弯降速 ＋ 软分离 ＋ 卡死换步</summary>
    /// <remarks>只吃"意图＋位置＋速度"，不碰场景。任何分支出不去方向时都必须交回非零的直线意图：零向量会被账本当"停住"，卡死判定就再也解不开。目标格由组合根每物理帧喂进 GridLogic；路径前视与元素格夹取在 EnemyPathFollower 里。</remarks>
    public sealed class EnemyNavigator
    {
        /// <summary>分离探针步长（格）：落点要跨进邻格，才判得出"推挤方向那一侧站不站得住"</summary>
        private const float ProbeCells = 1f;

        /// <summary>原始期望方向翻转判据（度）：只做诊断，不参与决策</summary>
        private const float DesiredSwitchDegrees = 20f;

        private readonly float _radius;
        private readonly EnemySteeringGate _gate;
        private readonly EnemyPathFollower _follower = new();
        private readonly List<Vector3Int> _neighborCells = new(8);

        private GridLogic _grid;
        private int _jamFrames;

        private Vector2 _lastDesired;
        private float _lastDesiredNow = float.NegativeInfinity;

        public EnemyNavigator(float radius, float turnRate, float turnSeconds)
        {
            _radius = radius > 0f ? radius : 0f;
            _gate = new EnemySteeringGate(turnRate, turnSeconds);
        }

        public int JamCount { get; private set; }

        /// <summary>原始期望方向相邻物理帧 >20° 的次数，诊断用</summary>
        public int DesiredSwitches { get; private set; }

        public bool IsJamming => _jamFrames > 0;

        public void AttachGrid(GridLogic grid)
        {
            _grid = grid;
        }

        public void NotifyStuck()
        {
            if (_grid == null) return;

            _jamFrames = ConfigModule.JamFrames;
            JamCount++;
        }

        /// <remarks>deceleration 是执行器的减速能力（MotionParams.MoveAcceleration）：刹车距离要按它算，拍脑袋的系数会在拐角处飘出走廊。
        /// now 由调用方给（闸门按它判同帧幂等）。</remarks>
        public Steering Resolve(EnemyIntent intent, Vector2 position, Vector2 velocity, float deceleration, float now)
        {
            if (intent.IsIdle)
            {
                _jamFrames = 0;
                ResetDesired(now);
                _gate.Reset(intent.Direction, now);

                return new Steering(intent.Direction, intent.Speed);
            }

            if (_grid == null || !_grid.Geometry.IsValid)
            {
                ResetDesired(now);
                _gate.Reset(intent.Direction, now);

                return new Steering(intent.Direction, intent.Speed);
            }

            float skin = ConfigModule.NavSkin;
            bool jamming = _jamFrames > 0;

            if (jamming) _jamFrames--;

            NavField field = _grid.NavFieldFor(_radius, skin);

            if (!_follower.TryResolve(
                    field,
                    _grid.Nav,
                    _grid,
                    position,
                    velocity,
                    jamming,
                    out Vector2 desired,
                    out float cornerDistance,
                    out bool cornerAhead))
            {
                // 场给不出一步（图外 / 不可达 / 已在目标）：交回直线意图，绝不给零向量
                ResetDesired(now);
                _gate.Reset(intent.Direction, now);

                return new Steering(intent.Direction, intent.Speed);
            }

            TrackDesired(desired, now);

            Vector2 dHat = desired.normalized;

            // 入弯降速：转弯半径 ∝ v²/a，满速 90° 转不过来，故对齐度越低目标速度越低
            float corner = ConfigModule.CornerSpeedFactor;
            float alignment = velocity.sqrMagnitude > 0f ? Vector2.Dot(velocity.normalized, dHat) : 1f;
            float speed = intent.Speed * (corner + (1f - corner) * Mathf.Clamp01(alignment));

            // 提前刹车：拐点在链上的那个节点，减速能力只够从 √(v_c² + 2ad) 降到 v_c
            if (cornerAhead && deceleration > 0f)
            {
                float cornerSpeed = corner * intent.Speed;
                float brake = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * deceleration * Mathf.Max(cornerDistance, 0f));

                if (brake < speed) speed = brake;
            }

            // 换步那几帧是在脱困：横向滑开还要减速只会更出不来
            if (jamming)
            {
                _gate.Reset(dHat, now);

                return new Steering(dHat, speed);
            }

            Vector2 push = ComputeSeparation(position, skin);

            if (push.sqrMagnitude <= 0f) return Emit(dHat, speed, velocity, now);

            Vector2 blended = dHat + push * ConfigModule.SeparationWeight;

            // 合成过小就只走流场方向：方向化成噪声会把怪送进墙里
            if (blended.sqrMagnitude < 0.01f) return Emit(dHat, speed, velocity, now);

            return Emit(blended.normalized, speed, velocity, now);
        }

        private Steering Emit(Vector2 direction, float speed, Vector2 velocity, float now)
        {
            return new Steering(_gate.Step(direction, velocity, now), speed);
        }

        private void ResetDesired(float now)
        {
            _lastDesired = Vector2.zero;
            _lastDesiredNow = now;
        }

        private void TrackDesired(Vector2 desired, float now)
        {
            // 同一物理帧内状态机与测试各调一次，只记第一次
            if (now <= _lastDesiredNow) return;

            if (_lastDesired.sqrMagnitude > 0f
                && desired.sqrMagnitude > 0f
                && Vector2.Angle(_lastDesired, desired) > DesiredSwitchDegrees)
            {
                DesiredSwitches++;
            }

            _lastDesired = desired;
            _lastDesiredNow = now;
        }

        /// <remarks>推挤方向的落点必须站得住，否则同伴会被推进墙里；生效距离 = 双方体半径之和 + separationRadius。</remarks>
        private Vector2 ComputeSeparation(Vector2 position, float skin)
        {
            EnemyCellRegistry registry = _grid.Registry;

            if (registry == null) return Vector2.zero;

            float margin = ConfigModule.SeparationRadius;
            Vector3Int own = _grid.WorldToCell(position);
            Vector2 push = Vector2.zero;

            Accumulate(registry, own, position, margin, ref push);

            GridQuery.GetNeighbors8(own, _neighborCells);

            for (int i = 0; i < _neighborCells.Count; i++)
            {
                Accumulate(registry, _neighborCells[i], position, margin, ref push);
            }

            if (push.sqrMagnitude <= 0f) return Vector2.zero;

            Vector2 probe = position + push.normalized * (_grid.Geometry.CellSize * ProbeCells);

            if (!EnemyPathFollower.IsPointWalkable(_grid, _grid.Nav, _radius, skin, probe)) return Vector2.zero;

            return push.sqrMagnitude > 1f ? push.normalized : push;
        }

        private void Accumulate(EnemyCellRegistry registry, Vector3Int cell, Vector2 position, float margin, ref Vector2 push)
        {
            if (!registry.TryGetIn(cell, out List<IEffectTarget> targets) || targets == null) return;

            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i] is IEnemyBody body)) continue;

                Vector2 delta = position - body.Position;
                float sqr = delta.sqrMagnitude;
                float reach = _radius + body.Radius + margin;

                if (sqr <= 0f || sqr >= reach * reach) continue;

                float distance = Mathf.Sqrt(sqr);

                push += delta / distance * (1f - distance / reach);
            }
        }
    }
}
