using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Drop;
using UnityEngine;

namespace DeepseaOil.Presentation.Drop
{
    /// <summary>
    /// 掉落物的调度器：<b>造 ＋ 持 ＋ 驱 ＋ 清</b>。
    /// </summary>
    /// <remarks>
    /// <b>它不判断掉落规则</b>（产什么 / 怎么产 / 何时产由产出方自己判定，见审查的口径）：
    /// 产出方（喷泉，将来的敌人死亡）拿到的是窄接口 <see cref="IDropSpawner"/>，
    /// 于是"谁能产出掉落物"这件事只写在装配期那一处。
    /// <para><b>为什么要有它：</b>产出方不止一处（现在一处、将来怪也会掉），
    /// 而"实例化 ＋ 注入玩家引用 ＋ 每帧驱动 ＋ 回收"这四件事只该有一份实现 ——
    /// 收口前这四件事全在 <c>Fountain</c> 里，于是"第二种掉落物"必须抄一遍喷泉。</para>
    /// <para><b>它不是 MonoBehaviour：</b>没有生命周期需求，由组合根显式造、显式驱动 ——
    /// 与 <c>BallDirector</c> 同一条纪律。</para>
    /// </remarks>
    public sealed class DropDirector : IDropSpawner
    {
        /// <summary>场上的掉落物（领取即回收，这里只做推进与清理）。</summary>
        private readonly List<DropActor> _drops = new List<DropActor>();

        /// <summary>种类 → 取值定义。</summary>
        private readonly Dictionary<DropType, DropDefinition> _definitions = new Dictionary<DropType, DropDefinition>();

        private Transform _root;
        private Transform _player;

        /// <summary>场上掉落物数量（诊断读数）。</summary>
        public int AliveCount => _drops.Count;

        /// <summary>装配是否完成（没接线时不产出，而不是产出追不到玩家的掉落物）。</summary>
        public bool IsReady => _player != null;

        /// <summary>
        /// 装配：注入父物体与玩家引用。
        /// </summary>
        /// <param name="root">掉落物的父物体；<c>null</c> 时建在场景根下。</param>
        /// <param name="player">玩家（"掉落物找玩家"用）；为 <c>null</c> 时本件停用。</param>
        public void Attach(Transform root, Transform player)
        {
            _root = root;
            _player = player;

            _definitions.Clear();

            // 加一种掉落物 ＝ 加一个 DropType 成员 ＋ 这一行 ＋ CreateActor 里一行
            DropDefinition water = DropCatalog.Water();

            _definitions[water.Type] = water;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 三个"拒绝"的理由各不相同，所以都<b>不静默</b>（除了"没接线"——那是装配错误，
        /// 组合根已经在装配日志里说过一次了）。
        /// </remarks>
        public bool TrySpawn(in DropSpawnRequest request)
        {
            if (_player == null) return false;

            if (!_definitions.TryGetValue(request.Type, out DropDefinition definition))
            {
                Debug.LogError($"[Drop] 没有 {request.Type} 的取值定义，这次产出被丢弃。");
                return false;
            }

            DropActor actor = CreateActor(request.Type);

            if (actor == null) return false;

            if (_root != null) actor.transform.SetParent(_root, true);

            actor.Initialize(in request, in definition, _player);

            _drops.Add(actor);

            return true;
        }

        /// <summary>推进一个渲染帧：驱动全部掉落物，把已领取的回收掉。</summary>
        /// <param name="deltaTime">本帧时长（<c>Time.deltaTime</c>）；暂停时为 0。</param>
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

                if (!drop.IsCollected) continue;

                Object.Destroy(drop.gameObject);
                _drops.RemoveAt(i);
            }
        }

        /// <summary>清空场上掉落物（打空重来 / 切场景）。</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _drops.Count; i++)
            {
                if (_drops[i] != null) Object.Destroy(_drops[i].gameObject);
            }

            _drops.Clear();
        }

        /// <summary>
        /// 组件工厂：种类 → 实体实现。
        /// </summary>
        /// <remarks>加一种掉落物在这里加一行（与特效的 <c>EffectDriverFactory</c> 同一条纪律）。
        /// 漏了实现时显式报错并丢掉这次产出，而不是留一个空物体在场上。</remarks>
        private static DropActor CreateActor(DropType type)
        {
            var go = new GameObject($"掉落物_{type}");

            switch (type)
            {
                case DropType.Water:
                    return go.AddComponent<WaterBallDrop>();

                default:
                    Debug.LogError($"[Drop] {type} 没有对应的掉落物实体，请在 CreateActor 里补一行。");
                    Object.Destroy(go);
                    return null;
            }
        }
    }
}
