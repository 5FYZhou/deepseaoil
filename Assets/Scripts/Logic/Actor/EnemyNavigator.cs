using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Grid;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>敌人转向器：流场跟随 ＋ 入弯降速 ＋ 软分离 ＋ 卡死换步</summary>
    /// <remarks>只吃"意图＋位置＋速度"，不碰场景。任何分支出不去方向时都必须交回非零的直线意图：零向量会被账本当"停住"，卡死判定就再也解不开。目标格由组合根每物理帧喂进 GridLogic。</remarks>
    public sealed class EnemyNavigator
    {
        /// <summary>分离探针步长（格）：落点要跨进邻格，才判得出"推挤方向那一侧站不站得住"</summary>
        private const float ProbeCells = 1f;

        /// <summary>链上往前看几个采样点：1.5 格，够覆盖一个拐角又不至于把远处当成近处</summary>
        private const int LookAheadNodes = 6;

        /// <summary>直线探针间距（采样点数）：0.5 格</summary>
        private const float ProbesPerNode = 2f;

        /// <summary>直线通路最多验几个探针：超出的远处交给流场逐帧重判</summary>
        private const int StraightProbeLimit = 12;

        /// <summary>判定"同向"的方向点积下限：四邻步只有同向（1）与转弯（0）两种，取 0.9 只躲浮点抖动</summary>
        private const float TurnCosine = 0.9f;

        private readonly float _radius;
        private readonly List<Vector3Int> _neighborCells = new(8);

        private GridLogic _grid;
        private int _jamFrames;
        private bool _straightMode;

        public EnemyNavigator(float radius)
        {
            _radius = radius > 0f ? radius : 0f;
        }

        public int JamCount { get; private set; }

        public bool IsJamming => _jamFrames > 0;

        /// <summary>L0 分诊：原始方向在直线/阶梯之间翻转的次数</summary>
        public int ModeSwitches { get; private set; }

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

        /// <remarks>deceleration 是执行器的减速能力（MotionParams.MoveAcceleration）：刹车距离要按它算，拍脑袋的系数会在拐角处飘出走廊。</remarks>
        public Steering Resolve(EnemyIntent intent, Vector2 position, Vector2 velocity, float deceleration)
        {
            if (intent.IsIdle)
            {
                _jamFrames = 0;

                return new Steering(intent.Direction, intent.Speed);
            }

            if (_grid == null || !_grid.Geometry.IsValid) return new Steering(intent.Direction, intent.Speed);

            float skin = ConfigModule.NavSkin;
            bool jamming = _jamFrames > 0;

            if (jamming) _jamFrames--;

            NavField field = _grid.NavFieldFor(_radius, skin);

            // 直线通路时直接压过去：只跟着场走会让开阔地上的对角线变成一格一格的阶梯。换步期间不用它（正卡着，直着走还是卡）
            Vector2 desired;
            float cornerDistance;
            bool cornerAhead;

            bool straight = !jamming && IsStraightDescend(field, position, position + intent.Direction);

            if (straight != _straightMode)
            {
                _straightMode = straight;
                ModeSwitches++;
            }

            if (straight)
            {
                desired = intent.Direction;
                cornerDistance = 0f;
                cornerAhead = false;
            }
            else if (!TryResolveFieldDirection(field, position, jamming, out desired, out cornerDistance, out cornerAhead))
            {
                // 场给不出一步（图外 / 不可达 / 已在目标）：交回直线意图，绝不给零向量
                return new Steering(intent.Direction, intent.Speed);
            }

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

            if (jamming) return new Steering(dHat, speed);

            Vector2 push = ComputeSeparation(position, skin);

            if (push.sqrMagnitude <= 0f) return new Steering(dHat, speed);

            Vector2 blended = dHat + push * ConfigModule.SeparationWeight;

            // 合成过小就只走流场方向：方向化成噪声会把怪送进墙里
            if (blended.sqrMagnitude < 0.01f) return new Steering(dHat, speed);

            return new Steering(blended.normalized, speed);
        }

        /// <summary>目标方向上的采样点都在场里且场距单调下降时，直线就是一条合法下坡：可以不走阶梯</summary>
        /// <remarks>用场距当净空探针（数组读，不查表也不查物理）：撞墙方向上的点必是 dist = -1，故撞墙那一支永远返回 false，卡死时不会又去顶同一面墙。
        /// 探针间距取两个采样点（0.5 格）：阻挡体再薄，只要横在两探针之间，就必有一端净空不达标。</remarks>
        private bool IsStraightDescend(NavField field, Vector2 position, Vector2 target)
        {
            NavMap map = _grid.Nav;
            float cellSize = _grid.Geometry.CellSize;
            Vector2 delta = target - position;
            float distance = delta.magnitude;

            if (map == null || distance <= cellSize) return true;

            float spacing = map.Spacing * ProbesPerNode;

            if (spacing <= 0f) return true;

            Vector2 dir = delta / distance;

            if (!map.TrySampleAt(position, out int previousIndex)) return false;

            int previous = field.DistOf(previousIndex);
            int samples = Mathf.Min(StraightProbeLimit, Mathf.CeilToInt(distance / spacing));

            for (int i = 1; i <= samples; i++)
            {
                Vector2 point = position + dir * (distance * i / samples);

                if (!map.TrySampleAt(point, out int index)) return false;

                // 落在同一个采样点上不算前进：本格的场距就等于 previous，会把整条直线误否掉
                if (index == previousIndex) continue;

                int dist = field.DistOf(index);

                if (dist < 0) return false;
                if (previous >= 0 && dist >= previous) return false;

                previousIndex = index;
                previous = dist;
            }

            return true;
        }

        private bool TryResolveFieldDirection(NavField field, Vector2 position, bool jamming, out Vector2 desired, out float cornerDistance, out bool cornerAhead)
        {
            desired = Vector2.zero;
            cornerDistance = 0f;
            cornerAhead = false;

            if (!TryGetStep(field, position, jamming, out int node, out int next)) return false;

            NavMap map = _grid.Nav;
            Vector2 waypoint = map.SampleCenter(next);
            Vector2 legHat = (waypoint - map.SampleCenter(node)).normalized;
            int cursor = next;
            int steps = 1;
            bool turned = false;

            // 沿链往前看：路标停在第一个拐点节点上，刹车必须在那之前完成
            while (steps < LookAheadNodes)
            {
                if (!field.TryGetStep(cursor, out int after)) break;

                Vector2 from = map.SampleCenter(cursor);
                Vector2 to = map.SampleCenter(after);
                Vector2 step = to - from;

                if (step.sqrMagnitude <= 0f) break;

                if (Vector2.Dot(step, legHat) < TurnCosine * step.magnitude)
                {
                    turned = true;

                    break;
                }

                cursor = after;
                waypoint = to;
                steps++;
            }

            desired = waypoint - position;

            if (desired.sqrMagnitude <= 0f) return false;

            // 换步那几帧是在脱困：横向滑开还要减速只会更出不来
            cornerAhead = turned && !jamming;
            cornerDistance = cornerAhead ? desired.magnitude : 0f;

            return true;
        }

        /// <remarks>平时取首选；本节点站不住（刷在墙里）或已在目标时退到"严格更近的邻节点"。换步优先取次优邻节点，四邻只剩首选时退回首选（贴墙也要往前压，不能原地不动）。</remarks>
        private bool TryGetStep(NavField field, Vector2 position, bool jamming, out int node, out int next)
        {
            next = -1;

            if (!field.TryFindStart(position, out node)) return false;

            next = node;

            return jamming
                ? field.TryGetStepOption(node, true, out next) || field.TryGetStep(node, out next)
                : field.TryGetStep(node, out next) || field.TryGetStepOption(node, false, out next);
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

            if (!IsPointWalkable(probe, skin)) return Vector2.zero;

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

        /// <summary>该点（不是整格）站不站得住：软分离落点与"整格判据"同源时，贴墙那半格会被漏掉</summary>
        private bool IsPointWalkable(Vector2 point, float skin)
        {
            NavMap map = _grid.Nav;

            if (map == null || !map.TrySampleAt(point, out int index)) return false;
            if (!map.IsSampleFree(index, _radius, skin)) return false;

            return !_grid.IsObstacle(_grid.WorldToCell(point));
        }
    }
}
