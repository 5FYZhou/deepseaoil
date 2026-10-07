using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>掉落物的调度器：造 ＋ 持 ＋ 驱 ＋ 清；它不判断掉落规则（产什么 / 怎么产 / 何时产由产出方自己判定）。</summary>
    /// <remarks>产出方只拿到窄接口 <see cref="IDropSpawner"/>，"谁能产出掉落物"这件事只写在装配期那一处；本类不是 MonoBehaviour，由组合根显式造、显式驱动。</remarks>
    public sealed class DropDirector : IDropSpawner
    {
        private readonly List<DropActor> _drops = new List<DropActor>();

        private readonly Dictionary<DropType, DropSpec> _definitions = new Dictionary<DropType, DropSpec>();

        private Transform _root;
        private Transform _player;

        public int AliveCount => _drops.Count;

        /// <summary>装配是否完成；没接线时不产出，而不是产出追不到玩家的掉落物。</summary>
        public bool IsReady => _player != null;

        // root 为 null 时掉落物建在场景根下；player 为 null 时本件停用（不产出）。
        public void Attach(Transform root, Transform player)
        {
            _root = root;
            _player = player;

            _definitions.Clear();

            // 加一种掉落物 ＝ 加一个 DropType 成员 ＋ 这一行 ＋ CreateActor 里一行
            DropSpec water = ConfigModule.GetDrop();

            _definitions[DropType.Water] = water;
        }

        /// <inheritdoc />
        /// <remarks>三个拒绝里只有"没接线"是静默的（装配错误，组合根已在装配日志里说过一次），其余都记日志。</remarks>
        public bool TrySpawn(in DropSpawnRequest request)
        {
            if (_player == null) return false;

            if (!_definitions.TryGetValue(request.Type, out DropSpec definition))
            {
                Debug.LogError($"[Drop] 没有 {request.Type} 的取值定义，这次产出被丢弃。");
                return false;
            }

            DropActor actor = CreateActor(request.Type);

            if (actor == null) return false;

            if (_root != null) actor.transform.SetParent(_root, true);

            actor.Initialize(in request, request.Type, definition, _player);

            _drops.Add(actor);

            return true;
        }

        public void Tick(float deltaTime)
        {
            // 倒序：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                DropActor drop = _drops[i];

                if (drop == null)
                {
                    _drops.RemoveAt(i);
                    continue;
                }

                drop.Tick(deltaTime);

                if (drop.IsAlive) continue;

                drop.Dispose();
                _drops.RemoveAt(i);
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _drops.Count; i++)
            {
                if (_drops[i] != null) _drops[i].Dispose();
            }

            _drops.Clear();
        }

        /// <remarks>加一种掉落物在这里加一行；漏了实现时显式报错并丢掉这次产出，而不是留一个空物体在场上。</remarks>
        private static DropActor CreateActor(DropType type)
        {
            var go = new GameObject($"Drop_{type}");

            switch (type)
            {
                case DropType.Water:
                    return go.AddComponent<WaterBallDrop>();

                default:
                    Debug.LogError($"[Drop] {type} 没有对应的掉落物实体，请在 CreateActor 里补一行。");
                    UnityEngine.Object.Destroy(go);
                    return null;
            }
        }
    }
}
