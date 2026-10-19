using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Logic.Combat;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>敌人 / 格上目标 → 格子的归属映射。单点判定（脚底中心），由目标自己每帧上报。</summary>
    /// <remarks>一格可站多个目标，故值是列表。注销是强制的：漏注销只留"看不见的尸体"。地貌 DoT 充能随归属注销摘除，换格不清零。</remarks>
    public sealed class EnemyCellRegistry
    {
        private readonly Dictionary<Vector3Int, List<IEffectTarget>> _byCell = new();
        private readonly Dictionary<IEffectTarget, Vector3Int> _cellOf = new();

        private readonly Dictionary<IEffectTarget, float> _dotCharge = new();

        /// <summary>登记到某格；已登记先摘旧登记</summary>
        public void Register(Vector3Int cell, IEffectTarget target)
        {
            if (target == null) return;

            if (!_dotCharge.ContainsKey(target)) _dotCharge[target] = 1f;

            if (_cellOf.TryGetValue(target, out Vector3Int previous))
            {
                if (previous == cell) return;   // 幂等：同格重复登记不产生第二份

                Detach(target, previous);
            }

            if (!_byCell.TryGetValue(cell, out List<IEffectTarget> list))
            {
                list = new List<IEffectTarget>(2);
                _byCell[cell] = list;
            }

            list.Add(target);
            _cellOf[target] = cell;
        }

        /// <summary>挪到新格；没登记过等价于登记</summary>
        public void Move(IEffectTarget target, Vector3Int cell)
        {
            if (target == null) return;

            if (_cellOf.TryGetValue(target, out Vector3Int previous) && previous == cell) return;

            Register(cell, target);
        }

        /// <summary>摘掉登记；没登记过是 no-op</summary>
        public void Unregister(IEffectTarget target)
        {
            if (target == null) return;

            if (!_cellOf.TryGetValue(target, out Vector3Int cell)) return;

            Detach(target, cell);

            // 换格经 Detach 走，只有注销才摘充能
            _dotCharge.Remove(target);
        }

        /// <summary>充能满则清零并返回 true</summary>
        public bool TryConsumeDotCharge(IEffectTarget target)
        {
            if (!_dotCharge.TryGetValue(target, out float charge) || charge < 1f) return false;

            _dotCharge[target] = 0f;

            return true;
        }

        /// <summary>逐帧推进充能，装满的连同当前格进 due</summary>
        public void AdvanceDotCharges(float deltaTime, List<(IEffectTarget Target, Vector3Int Cell)> due)
        {
            if (due == null) return;

            due.Clear();

            if (deltaTime <= 0f) return;

            // 结构体枚举器：经接口枚举会每帧装箱
            foreach (KeyValuePair<IEffectTarget, Vector3Int> pair in _cellOf)
            {
                IEffectTarget target = pair.Key;

                if (target == null || !target.IsAlive) continue;

                if (!_dotCharge.TryGetValue(target, out float charge)) continue;

                charge += deltaTime;

                _dotCharge[target] = charge > 1f ? 1f : charge;

                if (charge >= 1f) due.Add((target, pair.Value));
            }
        }

        /// <summary>取该格目标列表；遍历别动登记</summary>
        public bool TryGetIn(Vector3Int cell, out List<IEffectTarget> targets)
        {
            return _byCell.TryGetValue(cell, out targets);
        }

        private void Detach(IEffectTarget target, Vector3Int cell)
        {
            _cellOf.Remove(target);

            if (!_byCell.TryGetValue(cell, out List<IEffectTarget> list)) return;

            list.Remove(target);

            // 空列表删掉，否则占用读数偏大
            if (list.Count == 0) _byCell.Remove(cell);
        }
    }
}
