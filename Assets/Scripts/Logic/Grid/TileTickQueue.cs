using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>双缓冲的 Tick 队列：本帧提交的请求下一帧才处理。</summary>
    /// <remarks>
    /// 每个逻辑帧开头调一次 <see cref="Swap"/>，把"下一帧"翻成"本帧"；一次性消费 —— 处理完即从队列移除，想周期 Tick 必须在 <c>OnTick</c> 里重新提交（延迟由状态自己按 <c>ctx.Now</c> 管）。
    /// 同帧去重：一帧里被提交多次只算一次，少了它"两件事同时要求同一格 Tick"会让那一格在一帧里被算两遍（伤害也就翻倍）。
    /// </remarks>
    public sealed class TileTickQueue
    {
        private List<Vector3Int> _current = new();
        private List<Vector3Int> _next = new();
        private readonly HashSet<Vector3Int> _dedup = new();

        public IReadOnlyList<Vector3Int> Current => _current;

        public int Count => _current.Count;

        public void Schedule(Vector3Int cell)
        {
            if (_dedup.Add(cell)) _next.Add(cell);
        }

        public void Swap()
        {
            (_current, _next) = (_next, _current);

            _next.Clear();
            _dedup.Clear();
        }

        public void Clear()
        {
            _current.Clear();
            _next.Clear();
            _dedup.Clear();
        }
    }
}
