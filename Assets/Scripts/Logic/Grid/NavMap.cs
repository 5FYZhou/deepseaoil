using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>净空图：子格采样点到场景碰撞体的距离（世界单位），按体半径判可通行</summary>
    /// <remarks>判据是"身体站在某个采样点上会不会蹭墙"：栅栏常压在格心，只判格心会把地图整片判死。未登记格的采样点恒为 0；比较要拿 radius + skin 与饱和值比。采样间距必须小于最小 (radius + skin) 的两倍，否则薄墙能从两个采样点之间跨过去。</remarks>
    public sealed class NavMap
    {
        /// <summary>每格每轴的采样点数；间距 = 格宽 / Subdivision，改它就是换导航精度</summary>
        public const int Subdivision = 4;

        /// <summary>一格几个采样点</summary>
        public const int SamplesPerCell = Subdivision * Subdivision;

        private readonly GridGeometry _geometry;
        private readonly Vector2Int _min;
        private readonly Vector2Int _size;
        private readonly float[] _sample;

        public NavMap(Vector2Int min, Vector2Int size, in GridGeometry geometry)
        {
            _min = min;
            _size = size.x > 0 && size.y > 0 ? size : Vector2Int.zero;
            _geometry = geometry;

            _sample = new float[_size.x * _size.y * SamplesPerCell];
        }

        public bool IsValid => _size.x > 0 && _size.y > 0 && _geometry.IsValid;

        public int CellCount => _size.x * _size.y;

        public int SampleCount => _sample.Length;

        /// <summary>采样列数：四邻判据要用它挡"末列 +1 绕到下一行首列"</summary>
        public int SampleWidth => _size.x * Subdivision;

        /// <summary>采样间距（世界单位）</summary>
        public float Spacing => _geometry.IsValid ? _geometry.CellSize / Subdivision : 0f;

        public Vector2Int Min => _min;

        public Vector2Int Size => _size;

        public bool TryIndex(Vector3Int cell, out int index)
        {
            int x = cell.x - _min.x;
            int y = cell.y - _min.y;

            if (x < 0 || y < 0 || x >= _size.x || y >= _size.y)
            {
                index = -1;

                return false;
            }

            index = x + y * _size.x;

            return true;
        }

        public Vector3Int CellOf(int index)
        {
            if (index < 0 || index >= CellCount) return default;

            return new Vector3Int(_min.x + index % _size.x, _min.y + index / _size.x, 0);
        }

        public int SampleIndexOfCell(int index)
        {
            return SampleIndexOf(index, 0, 0);
        }

        public int CellIndexOfSample(int sampleIndex)
        {
            if (sampleIndex < 0 || sampleIndex >= _sample.Length) return -1;

            int width = SampleWidth;
            int x = sampleIndex % width;
            int y = sampleIndex / width;

            return x / Subdivision + y / Subdivision * _size.x;
        }

        public Vector3Int CellOfSample(int sampleIndex)
        {
            return CellOf(CellIndexOfSample(sampleIndex));
        }

        public bool TrySampleIndex(Vector3Int cell, out int sampleIndex)
        {
            sampleIndex = TryIndex(cell, out int index) ? SampleIndexOf(index, 0, 0) : -1;

            return sampleIndex >= 0;
        }

        /// <remarks>必须是**采样坐标的行主序**：流场按 ±1 / ±SampleWidth 走四邻，格内分块布局会让"相邻"落到别的格上。</remarks>
        public int SampleIndexOf(int cellIndex, int i, int j)
        {
            if (cellIndex < 0 || cellIndex >= CellCount) return -1;
            if (i < 0 || j < 0 || i >= Subdivision || j >= Subdivision) return -1;

            int x = cellIndex % _size.x * Subdivision + i;
            int y = cellIndex / _size.x * Subdivision + j;

            return x + y * SampleWidth;
        }

        public void SetSampleClearance(int sampleIndex, float clearance)
        {
            if (sampleIndex < 0 || sampleIndex >= _sample.Length) return;

            _sample[sampleIndex] = clearance > 0f ? clearance : 0f;
        }

       public void SetCellClearance(int index, float clearance)
        {
            float value = clearance > 0f ? clearance : 0f;

            for (int j = 0; j < Subdivision; j++)
            {
                for (int i = 0; i < Subdivision; i++) SetSampleClearance(SampleIndexOf(index, i, j), value);
            }
        }

        public float SampleClearance(int sampleIndex)
        {
            return sampleIndex >= 0 && sampleIndex < _sample.Length ? _sample[sampleIndex] : 0f;
        }

        /// <summary>格内最大净空：这格里有没有站得住的地方。判"这格能不能去"只该读它</summary>
        public float CellClearance(int index)
        {
            if (index < 0 || index >= CellCount) return 0f;

            float best = 0f;

            for (int j = 0; j < Subdivision; j++)
            {
                for (int i = 0; i < Subdivision; i++)
                {
                    float value = SampleClearance(SampleIndexOf(index, i, j));

                    if (value > best) best = value;
                }
            }

            return best;
        }

        public float CellClearance(Vector3Int cell)
        {
            return TryIndex(cell, out int index) ? CellClearance(index) : 0f;
        }

        public bool IsSampleFree(int sampleIndex, float radius, float skin)
        {
            return SampleClearance(sampleIndex) > radius + skin;
        }

        /// <summary>该格有没有能站的地方；"没有"不等于整格都是墙</summary>
        public bool IsWalkable(Vector3Int cell, float radius, float skin)
        {
            return CellClearance(cell) > radius + skin;
        }

        public Vector2 SampleCenter(int sampleIndex)
        {
            if (sampleIndex < 0 || sampleIndex >= _sample.Length || !_geometry.IsValid) return Vector2.zero;

            float size = _geometry.CellSize;
            float step = size / Subdivision;
            int width = SampleWidth;
            int x = sampleIndex % width;
            int y = sampleIndex / width;

            return new Vector2(
                _geometry.Origin.x + _min.x * size + (x + 0.5f) * step,
                _geometry.Origin.y + _min.y * size + (y + 0.5f) * step);
        }

        public Vector2 CellCenter(Vector3Int cell)
        {
            return _geometry.CellCenter(cell);
        }

        /// <remarks>必须与 WorldToCell 同源：先由格换算得格坐标，再用格内偏移定位采样点，负坐标才不会错一行。</remarks>
        public bool TrySampleAt(Vector2 world, out int sampleIndex)
        {
            sampleIndex = -1;

            if (!IsValid) return false;

            Vector3Int cell = _geometry.WorldToCell(world);

            if (!TryIndex(cell, out int cellIndex)) return false;

            int i = Mathf.FloorToInt((world.x - _geometry.Origin.x) / _geometry.CellSize * Subdivision) - cell.x * Subdivision;
            int j = Mathf.FloorToInt((world.y - _geometry.Origin.y) / _geometry.CellSize * Subdivision) - cell.y * Subdivision;

            i = Mathf.Clamp(i, 0, Subdivision - 1);
            j = Mathf.Clamp(j, 0, Subdivision - 1);
            sampleIndex = SampleIndexOf(cellIndex, i, j);

            return true;
        }
    }
}
