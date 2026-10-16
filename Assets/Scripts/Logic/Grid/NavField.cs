using System;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>单半径的流向场：以目标格为源的 BFS 父指针，节点是子格采样点</summary>
    /// <remarks>四邻、单位代价：节点位移恒为轴对齐，故不斜穿墙角；薄墙也跨不过去（见 NavMap 的采样间距约束）。不可达节点 distance = -1，调用方对不可达要退回直线压进而不是原地打转。数组一次性分配，重建是纯循环、零分配。</remarks>
    public sealed class NavField
    {
        /// <summary>种子外搜半径：玩家贴栅栏站会让所在格净空不足，种子必须外搜</summary>
        private const int SeedRing = 2;

        private const int StartRing = 1;

        private readonly int[] _offsets = new int[4];

        private NavMap _map;
        private int[] _dist = Array.Empty<int>();
        private int[] _next = Array.Empty<int>();
        private int[] _queue = Array.Empty<int>();

        /// <summary>该采样点站不站得住（0/1）；与 _dist 分开，脱困时要用"站不住的节点"问邻格</summary>
        private byte[] _free = Array.Empty<byte>();

        /// <summary>该格是不是阻挡地貌（0/1）：按格缓存，内层按采样点查表会翻 16 倍</summary>
        private byte[] _blocked = Array.Empty<byte>();

        private int _width = 1;

        public NavField(float radius)
        {
            Radius = radius;
        }

        public float Radius { get; }

        public float Skin { get; private set; }

        public int Version { get; internal set; } = -1;

        public int RebuildCount { get; private set; }

        /// <summary>本场覆盖的可达采样点数（含种子）；0 = 目标被完全围死</summary>
        public int ReachableCount { get; private set; }

        public Vector3Int Goal { get; private set; }

        /// <summary>按当前目标格重建整场；目标与所有阻挡都由参数决定，故重建是纯函数</summary>
        public void Build(NavMap map, GridLogic grid, Vector3Int goalCell, float skin)
        {
            RebuildCount++;

            Goal = goalCell;
            Skin = skin;
            _map = map;

            if (map == null || !map.IsValid || grid == null)
            {
                ReachableCount = 0;

                return;
            }

            _width = map.SampleWidth;

            _offsets[0] = -1;
            _offsets[1] = 1;
            _offsets[2] = -_width;
            _offsets[3] = _width;

            int count = map.SampleCount;

            EnsureCapacity(count);

            _free = _free.Length >= count ? _free : new byte[count];
            _blocked = _blocked.Length >= map.CellCount ? _blocked : new byte[map.CellCount];

            for (int i = 0; i < map.CellCount; i++)
            {
                // 一格一次：IsObstacle 要查两张表，放进采样点内层会按 16 倍翻
                _blocked[i] = grid.IsObstacle(map.CellOf(i)) ? (byte)1 : (byte)0;
            }

            for (int i = 0; i < count; i++)
            {
                _free[i] = map.SampleClearance(i) > Radius + Skin && _blocked[map.CellIndexOfSample(i)] == 0 ? (byte)1 : (byte)0;
            }

            Array.Fill(_dist, -1);

            // _next 必须一起清：换步与同向判据都会读它，留着上次构建的父指针会把邻节点误排除
            Array.Fill(_next, -1);

            int seed = ResolveSeed(map, goalCell);

            if (seed < 0)
            {
                ReachableCount = 0;

                return;
            }

            int head = 0;
            int tail = 0;

            _dist[seed] = 0;
            _next[seed] = -1;
            _queue[tail++] = seed;

            while (head < tail)
            {
                int current = _queue[head++];

                for (int n = 0; n < 4; n++)
                {
                    int next = current + _offsets[n];

                    if (next < 0 || next >= count) continue;

                    // 横向偏移只在同一行内合法：末列 +1 会绕到下一行首列
                    if (n < 2 && current / _width != next / _width) continue;

                    if (_free[next] == 0) continue;
                    if (_dist[next] >= 0) continue;

                    _dist[next] = _dist[current] + 1;
                    _next[next] = current;
                    _queue[tail++] = next;
                }
            }

            ReachableCount = tail;
        }

        public bool TryGetStep(int index, out int next)
        {
            next = index;

            if (index < 0 || index >= _dist.Length) return false;
            if (_dist[index] < 0) return false;

            int parent = _next[index];

            if (parent < 0) return false;

            next = parent;

            return true;
        }

        /// <summary>四邻里场距更小的一步；skipPrimary 排除首选（卡死换步）</summary>
        /// <remarks>本节点站不住时不要求场距，否则被击退进阻挡格的怪永远出不来。换步那一路（skipPrimary）分两轮：先只认"严格更近"的邻节点，空手就放开距离要求允许横向/后退一步脱困 —— 开阔地上唯一的改善方向就是首选，只认更近等于换步换不出去（会一直顶同一条边）。
        /// 任何一轮都只走站得住的邻节点，同距时优先与首选同向的邻节点：贴墙卡住时横向滑比原路倒退更容易脱出。</remarks>
        public bool TryGetStepOption(int index, bool skipPrimary, out int next)
        {
            next = index;

            if (index < 0 || index >= _dist.Length) return false;

            bool selfFree = IsUsable(index, false);
            int selfDist = _dist[index];
            int primary = _next[index];
            int skip = skipPrimary ? primary : -1;

            // 本节点站得住时先只认严格更近；站不住（在阻挡里）时挑场距最小的邻节点挤出
            int limit = selfFree && selfDist >= 0 ? selfDist : int.MaxValue;
            int best = Pick(index, selfFree, limit, skip);

            if (best < 0 && skipPrimary) best = Pick(index, selfFree, int.MaxValue, skip);

            if (best < 0) return false;

            next = best;

            return true;
        }

        /// <summary>身体位置对应的出发节点：所在采样点可用就用它，否则在 StartRing 圈内找最近的</summary>
        /// <remarks>两轮：先要有场距的（跟着场走），一轮空手才退到"只要站得住"（被击退进阻挡格时靠它挤出来）。找不到落点就只剩直线意图，故这里不返回 _dist = -1 的节点当出发点。</remarks>
        public bool TryFindStart(Vector2 position, out int index)
        {
            index = -1;

            if (_map == null || !_map.IsValid) return false;
            if (!_map.TrySampleAt(position, out int own)) return false;

            if (IsUsable(own, true))
            {
                index = own;

                return true;
            }

            Vector3Int ownCell = _map.CellOfSample(own);
            int found = ScanStart(position, ownCell, true);

            if (found < 0) found = ScanStart(position, ownCell, false);

            index = found;

            return found >= 0;
        }

        public int DistOf(int index)
        {
            return index >= 0 && index < _dist.Length ? _dist[index] : -1;
        }

        public int CellDistOf(Vector3Int cell)
        {
            if (_map == null || !_map.TryIndex(cell, out int cellIndex)) return -1;

            int best = -1;

            for (int j = 0; j < NavMap.Subdivision; j++)
            {
                for (int i = 0; i < NavMap.Subdivision; i++)
                {
                    int index = _map.SampleIndexOf(cellIndex, i, j);

                    if (index < 0 || index >= _dist.Length) continue;

                    int dist = _dist[index];

                    if (dist < 0) continue;
                    if (best < 0 || dist < best) best = dist;
                }
            }

            return best;
        }

        private int Pick(int index, bool selfFree, int limit, int skip)
        {
            int primary = _next[index];
            int bestDist = int.MaxValue;
            int bestAlign = int.MinValue;
            int best = -1;

            for (int n = 0; n < 4; n++)
            {
                int candidate = index + _offsets[n];

                if (candidate < 0 || candidate >= _dist.Length) continue;
                if (n < 2 && index / _width != candidate / _width) continue;
                if (candidate == skip) continue;
                if (!IsUsable(candidate, false)) continue;
                if (_dist[candidate] >= limit) continue;

                int align = Alignment(index, candidate, primary);

                if (_dist[candidate] > bestDist) continue;
                if (_dist[candidate] == bestDist && align <= bestAlign) continue;

                bestDist = _dist[candidate];
                bestAlign = align;
                best = candidate;
            }

            return best;
        }

        private int Alignment(int index, int candidate, int primary)
        {
            if (primary < 0) return 0;

            int ax = candidate % _width - index % _width;
            int ay = candidate / _width - index / _width;
            int bx = primary % _width - index % _width;
            int by = primary / _width - index / _width;

            return ax * bx + ay * by;
        }

        /// <summary>种子采样点：先在本格里挑（取最靠格心的可站点），本格全站不住再外搜 SeedRing 圈</summary>
        private int ResolveSeed(NavMap map, Vector3Int goalCell)
        {
            if (map.TryIndex(goalCell, out int cellIndex))
            {
                int own = NearestFreeSample(cellIndex);

                if (own >= 0) return own;
            }

            int best = -1;
            int bestCell = int.MaxValue;

            for (int dy = -SeedRing; dy <= SeedRing; dy++)
            {
                for (int dx = -SeedRing; dx <= SeedRing; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int cellScore = dx * dx + dy * dy;

                    if (cellScore > bestCell) continue;

                    var cell = new Vector3Int(goalCell.x + dx, goalCell.y + dy, 0);

                    if (!map.TryIndex(cell, out int ringIndex)) continue;

                    int candidate = NearestFreeSample(ringIndex);

                    if (candidate < 0) continue;

                    bestCell = cellScore;
                    best = candidate;
                }
            }

            return best;
        }

        private int NearestFreeSample(int cellIndex)
        {
            int best = -1;
            int bestScore = int.MaxValue;
            int middle = NavMap.Subdivision - 1;

            for (int j = 0; j < NavMap.Subdivision; j++)
            {
                for (int i = 0; i < NavMap.Subdivision; i++)
                {
                    int index = _map.SampleIndexOf(cellIndex, i, j);

                    if (index < 0 || index >= _free.Length || _free[index] == 0) continue;

                    int score = Mathf.Abs(i * 2 - middle) + Mathf.Abs(j * 2 - middle);

                    if (score >= bestScore) continue;

                    bestScore = score;
                    best = index;
                }
            }

            return best;
        }

        private int ScanStart(Vector2 position, Vector3Int center, bool needField)
        {
            int best = -1;
            float bestSqr = float.MaxValue;

            for (int dy = -StartRing; dy <= StartRing; dy++)
            {
                for (int dx = -StartRing; dx <= StartRing; dx++)
                {
                    var cell = new Vector3Int(center.x + dx, center.y + dy, 0);

                    if (!_map.TryIndex(cell, out int cellIndex)) continue;

                    for (int j = 0; j < NavMap.Subdivision; j++)
                    {
                        for (int i = 0; i < NavMap.Subdivision; i++)
                        {
                            int candidate = _map.SampleIndexOf(cellIndex, i, j);

                            if (!IsUsable(candidate, needField)) continue;

                            float sqr = (_map.SampleCenter(candidate) - position).sqrMagnitude;

                            if (sqr >= bestSqr) continue;

                            bestSqr = sqr;
                            best = candidate;
                        }
                    }
                }
            }

            return best;
        }

        private bool IsUsable(int index, bool needField)
        {
            if (index < 0 || index >= _free.Length || _free[index] == 0) return false;

            return !needField || (index < _dist.Length && _dist[index] >= 0);
        }

        private void EnsureCapacity(int count)
        {
            if (_dist.Length < count) _dist = new int[count];
            if (_next.Length < count) _next = new int[count];
            if (_queue.Length < count) _queue = new int[count];
        }
    }
}
