using System.Collections.Generic;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Element
{
    /// <summary>一次元素反应的结果：切到哪个状态 ＋ 进格提交的效果清单</summary>
    public readonly struct ElementReaction
    {
        public readonly TileStateType Next;

        public readonly IReadOnlyList<TileEffectValue> Effects;

        /// <summary>是否有规则真的命中，false = 走的是规则表最后一条兜底行</summary>
        public readonly bool Matched;

        public ElementReaction(TileStateType next, IReadOnlyList<TileEffectValue> effects, bool matched)
        {
            Next = next;
            Effects = effects;
            Matched = matched;
        }
    }

    /// <summary>元素层端口，持每格元素，负责"球元素 ⊕ 地形元素 → 查规则 → 结果"</summary>
    /// <remarks>元素是格子属性，不是状态的一部分，状态机可零常驻。切进状态时把该状态元素刷到格上作初值。地形改写走 SetElement。</remarks>
    public interface IElementReactor
    {
        /// <summary>取某格当前元素，无记录时给全零元素</summary>
        ElementValue GetElement(Vector3Int cell);

        /// <summary>改写某格元素，全零时摘掉该格记录</summary>
        void SetElement(Vector3Int cell, in ElementValue element);

        void FlushStateElement(Vector3Int cell, in TileStateSpec spec);

        /// <summary>结算一次落地：读该格元素 → 与球元素合成 → 写回该格 → 查规则</summary>
        ElementReaction React(Vector3Int cell, in ElementValue ballElement, in TileStateSpec currentSpec);
    }

    /// <summary>元素层实现：元素合成 ElementCombiner ＋ 规则匹配 ReactionResolver ＋ 每格元素的持有</summary>
    /// <remarks>由组合根构造注入给格子层，不吃单例。本层不认识格子层，只吃格坐标 ＋ 两份元素 ＋ 状态包装件，产出下一状态 ＋ 效果清单。纯 C#，不碰 MonoBehaviour / Time / Physics2D。</remarks>
    public sealed class TileElementReactor : IElementReactor
    {
        private readonly Dictionary<Vector3Int, ElementValue> _elements = new();

        private readonly IReadOnlyList<ElementRuleSpec> _rules;

        /// <summary>元素反应规则，顺序即优先级；null 或空时任何反应都不发生</summary>
        public TileElementReactor(IReadOnlyList<ElementRuleSpec> rules)
        {
            _rules = rules;
        }

        /// <summary>当前有元素记录的格数，诊断用</summary>
        public int ElementCellCount => _elements.Count;

        public ElementValue GetElement(Vector3Int cell)
        {
            return _elements.TryGetValue(cell, out ElementValue value) ? value : default;
        }

        public void SetElement(Vector3Int cell, in ElementValue element)
        {
            if (element.IsEmpty)
            {
                _elements.Remove(cell);
                return;
            }

            _elements[cell] = element;
        }

        /// <summary>切进某状态时把该状态元素刷到格上作初值</summary>
        /// <remarks>判据只看元素是否为空，不看状态是否 Normal：常规格表自己的元素也要刷得上去，那片地的脾性写在 tile_state 的 Normal 行里。空元素才摘记录，留全零条目只会让常驻内存长大。</remarks>
        public void FlushStateElement(Vector3Int cell, in TileStateSpec spec)
        {
            SetElement(cell, spec.Element);
        }

        /// <summary>结算一次落地并写回该格元素</summary>
        /// <remarks>规则未命中且状态不变时调用方不刷新初值，须自行收尾。</remarks>
        public ElementReaction React(Vector3Int cell, in ElementValue ballElement, in TileStateSpec currentSpec)
        {
            ElementValue combined = ElementCombiner.Combine(in ballElement, GetElement(cell));

            SetElement(cell, in combined);

            bool matched = ReactionResolver.Match(_rules, in combined, out TileStateType next, out IReadOnlyList<TileEffectValue> effects);

            if (!matched && (effects == null || effects.Count == 0))
            {
                // 规则表为空（或最后一条也没有效果）：什么都不做，元素改动由调用方按"状态没变"收尾。
                return new ElementReaction(TileStateType.None, null, false);
            }

            return new ElementReaction(next, effects, matched);
        }

        /// <summary>清空全部元素记录，随格子复位</summary>
        public void Clear()
        {
            _elements.Clear();
        }
    }
}
