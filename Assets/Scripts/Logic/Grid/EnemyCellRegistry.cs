using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Combat;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>敌人 → 格子 的归属映射。<b>单点判定</b>（脚底中心），由敌人自己每帧上报。</summary>
    /// <remarks>注销是强制的：表里存的是引用，敌人被销毁而没注销就会留下查得到但不能用的条目 —— 漏注销的表现是"格子上一具看不见的尸体"，而不是 <c>MissingReferenceException</c>。一格可以站多个目标（将来的大体型 / 重叠），所以值是列表。</remarks>
    public sealed class EnemyCellRegistry
    {
        private readonly Dictionary<Vector3Int, List<IDamageable>> _byCell = new();
        private readonly Dictionary<IDamageable, Vector3Int> _cellOf = new();

        public int Count => _cellOf.Count;

        /// <summary>登记一个目标到某格；已在别处登记时会先摘掉旧登记。</summary>
        public void Register(Vector3Int cell, IDamageable target)
        {
            if (target == null) return;

            if (_cellOf.TryGetValue(target, out Vector3Int previous))
            {
                if (previous == cell) return;   // 幂等：同一格重复登记不产生第二份

                Detach(target, previous);
            }

            if (!_byCell.TryGetValue(cell, out List<IDamageable> list))
            {
                list = new List<IDamageable>(2);
                _byCell[cell] = list;
            }

            list.Add(target);
            _cellOf[target] = cell;
        }

        /// <summary>把目标挪到新格；没登记过则等价于登记。</summary>
        public void Move(IDamageable target, Vector3Int cell)
        {
            if (target == null) return;

            if (_cellOf.TryGetValue(target, out Vector3Int previous) && previous == cell) return;

            Register(cell, target);
        }

        /// <summary>摘掉一个目标的登记；没登记过是 no-op。</summary>
        public void Unregister(IDamageable target)
        {
            if (target == null) return;

            if (!_cellOf.TryGetValue(target, out Vector3Int cell)) return;

            Detach(target, cell);
        }

        /// <summary>取该格上的目标列表。<b>不要在遍历它的过程中调用 Register / Unregister</b>。</summary>
        public bool TryGetIn(Vector3Int cell, out List<IDamageable> targets)
        {
            return _byCell.TryGetValue(cell, out targets);
        }

        public void Clear()
        {
            _byCell.Clear();
            _cellOf.Clear();
        }

        private void Detach(IDamageable target, Vector3Int cell)
        {
            _cellOf.Remove(target);

            if (!_byCell.TryGetValue(cell, out List<IDamageable> list)) return;

            list.Remove(target);

            // 空列表要删掉：留着空 List 的格子在"这一格有没有东西"上是真话，但在"场上还有几个格被占用"上是假话，而后者是排查泄漏时唯一看得懂的读数。
            if (list.Count == 0) _byCell.Remove(cell);
        }
    }
}
