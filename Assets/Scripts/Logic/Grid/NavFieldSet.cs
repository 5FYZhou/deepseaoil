using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>按体半径分场的流向场集合：目标或阻挡一变，版本 +1，落后的场按当前目标重建</summary>
    /// <remarks>每种怪一张场：净空判据含体半径，共用一张会让胖怪走上它挤不过去的路。场全部由目标格与阻挡唯一决定，故重建是纯函数。</remarks>
    public sealed class NavFieldSet
    {
        private const float RadiusEpsilon = 1e-4f;

        private readonly List<NavField> _fields = new(4);

        private Vector3Int _goal;
        private float _skin;
        private float _elementCost = 0.85f;
        private bool _hasGoal;
        private int _version;

        /// <summary>地形快照版本：只在阻挡/元素/换图时 +1；换目标格不动它，场的可站集合才能跨重建复用</summary>
        private int _terrainVersion;

        public int FieldCount => _fields.Count;

        /// <summary>按序号取场（诊断画图用）；越界返回 null</summary>
        public NavField FieldAt(int index)
        {
            return index >= 0 && index < _fields.Count ? _fields[index] : null;
        }

        /// <summary>累计重建次数，诊断用；该随"玩家跨格次数"涨，不是随帧数</summary>
        public int TotalRebuilds { get; private set; }

        public bool HasGoal => _hasGoal;

        public Vector3Int Goal => _goal;

        /// <summary>阻挡或合法格变了：全部场作废（不改目标格）</summary>
        public void MarkDirty()
        {
            _version++;
            _terrainVersion++;
        }

        /// <summary>喂目标格、皮厚与元素步价：都没变就不作废，于是站着不动的玩家不引起重建</summary>
        public void SetGoal(Vector3Int goal, float skin, float elementCost)
        {
            if (_hasGoal
                && goal == _goal
                && skin == _skin
                && Mathf.Approximately(elementCost, _elementCost)) return;

            _goal = goal;
            _skin = skin;
            _elementCost = elementCost;
            _hasGoal = true;
            _version++;
        }

        /// <summary>把已存在的场都追到当前版本；没有目标格时 no-op</summary>
        public void RebuildStale(GridLogic grid, NavMap map)
        {
            if (!_hasGoal) return;

            for (int i = 0; i < _fields.Count; i++)
            {
                Rebuild(_fields[i], grid, map);
            }
        }

        /// <summary>取该半径的场；该半径第一次出现时现建一张（每种怪一次）</summary>
        public NavField Get(float radius, float skin, GridLogic grid, NavMap map)
        {
            NavField field = Find(radius);

            if (field == null)
            {
                field = new NavField(radius);
                _fields.Add(field);
            }

            // 皮厚或元素步价参与判据，换了就等于所有场作废（调用方传的是同一份调参，正常不会抖）
            if (!Mathf.Approximately(skin, _skin))
            {
                _skin = skin;
                _version++;
            }

            if (!Mathf.Approximately(grid.NavElementCost, _elementCost))
            {
                _elementCost = grid.NavElementCost;
                _version++;
            }

            if (_hasGoal) Rebuild(field, grid, map);

            return field;
        }

        private void Rebuild(NavField field, GridLogic grid, NavMap map)
        {
            if (field.Version == _version) return;

            field.Build(map, grid, _goal, _skin, _elementCost, _terrainVersion);
            field.Version = _version;
            TotalRebuilds++;
        }

        private NavField Find(float radius)
        {
            for (int i = 0; i < _fields.Count; i++)
            {
                if (Mathf.Abs(_fields[i].Radius - radius) <= RadiusEpsilon) return _fields[i];
            }

            return null;
        }
    }
}
