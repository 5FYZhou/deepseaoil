using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>
    /// 双缓冲的 Tick 队列：<b>本帧提交的请求下一帧才处理</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么必须双缓冲：</b>状态的 <c>OnTick</c> 会提交下一次 Tick。若提交当场生效，
    /// "周期 Tick"就变成同帧递归（一个状态能把一整帧耗光），而且提交顺序会决定谁先跑 ——
    /// 那正是"格子的行为依赖遍历顺序"这类不可复现缺陷的温床。
    /// <para><b>一次性消费：</b>处理完即从队列移除。状态想周期 Tick 必须在 <c>OnTick</c> 里重新提交。
    /// 延迟（"我该在 0.5 秒后再算一次"）由状态自己用 <c>ctx.Now</c> / <c>ctx.DeltaTime</c> 管理 ——
    /// 队列只回答"哪些格下一帧要 Tick"，不回答"什么时候"。</para>
    /// <para><b>同帧去重：</b>一帧里被提交多次只算一次。少了它，"两件事同时要求同一格 Tick"
    /// 会让那一格在一帧里被算两遍（伤害也就翻倍）。</para>
    /// </remarks>
    public sealed class TileTickQueue
    {
        // 两个列表不是 readonly：Swap 走的是"换引用"而不是"搬元素"，
        // 换引用是 O(1) 且不产生垃圾；代价只是这两个字段不能标 readonly。
        private List<Vector3Int> _current = new();
        private List<Vector3Int> _next = new();
        private readonly HashSet<Vector3Int> _dedup = new();

        /// <summary>本帧要处理的格（只读；不要在遍历中改它）。</summary>
        public IReadOnlyList<Vector3Int> Current => _current;

        /// <summary>本帧待处理数。</summary>
        public int Count => _current.Count;

        /// <summary>提交一次 Tick 请求（下一帧生效，同帧去重）。</summary>
        public void Schedule(Vector3Int cell)
        {
            if (_dedup.Add(cell)) _next.Add(cell);
        }

        /// <summary>翻页：把"下一帧"变成"本帧"。每个逻辑帧开头调用一次。</summary>
        public void Swap()
        {
            (_current, _next) = (_next, _current);

            _next.Clear();
            _dedup.Clear();
        }

        /// <summary>清空两侧队列（切场景 / 重新灌格子时用）。</summary>
        public void Clear()
        {
            _current.Clear();
            _next.Clear();
            _dedup.Clear();
        }
    }
}
