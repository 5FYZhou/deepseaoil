using System.Collections.Generic;
using UnityEngine;
using cfg.demo;
using DeepseaOil.Data;
using DeepseaOil.Logic.Element;

namespace DeepseaOil.Logic.Grid
{
    /// <summary>一次元素反应的结果：切到哪个状态 ＋ 进格时要提交的效果清单。</summary>
    public readonly struct ElementReaction
    {
        public readonly TileStateType Next;

        public readonly IReadOnlyList<TileEffectValue> Effects;

        /// <summary>是否有规则真的命中（<c>false</c> = 走的是规则表的最后一条兜底行）。</summary>
        public readonly bool Matched;

        public ElementReaction(TileStateType next, IReadOnlyList<TileEffectValue> effects, bool matched)
        {
            Next = next;
            Effects = effects;
            Matched = matched;
        }
    }

    /// <summary>
    /// <b>元素层的端口</b>：持每格的元素，并负责"球元素 ⊕ 地形元素 → 查规则 → 结果"。
    /// </summary>
    /// <remarks>
    /// 元素是<b>格子的一块属性</b>，不是状态的一部分（§7 关卡 3 拍板）：状态机因此仍可零常驻，<c>duration = -1</c> 的永久地块不必持有状态机。
    /// <b>D9</b>：切进某状态时把该状态的元素四件刷到格子上作初值（<see cref="FlushStateElement"/>）。
    /// <b>D11</b>：地形改写（温湿度继承 / 清除植物）走 <see cref="SetElement"/> —— 那是"执行者代效果改格"的通道，效果实现自己仍然不碰别的格（D4）。
    /// </remarks>
    public interface IElementReactor
    {
        /// <summary>取某格当前元素；没记录过（＝还没被任何反应写过）时给一份全零元素。</summary>
        ElementValue GetElement(Vector3Int cell);

        /// <summary>改写某格元素（D11 通道）：全零时把这一格的记录摘掉，不占常驻内存。</summary>
        void SetElement(Vector3Int cell, in ElementValue element);

        /// <summary>D9：切进某状态时，把该状态的元素四件刷到格子上作初值。</summary>
        /// <remarks>落回 <see cref="TileStateType.Normal"/> 时是"把这一格的元素记录摘掉" —— 常规格不该留着上一把火留下的温度。</remarks>
        void FlushStateElement(Vector3Int cell, in TileStateSpec spec);

        /// <summary>结算一次落地：读该格元素 → 与球元素合成 → 写回该格（合成结果即该格当下元素）→ 查规则。</summary>
        ElementReaction React(Vector3Int cell, in ElementValue ballElement, in TileStateSpec currentSpec);
    }

    /// <summary>
    /// 元素层的实现：<b>元素合成（<see cref="ElementCombiner"/>）＋ 规则匹配（<see cref="ReactionResolver"/>）＋ 每格元素的持有</b>。
    /// </summary>
    /// <remarks>
    /// 由组合根装配后构造注入给 <see cref="GridLogic"/>（§8：与 <c>stateSpecs</c> 同一待遇，不吃单例）；
    /// <c>GridLogic</c> 因此只留"状态 ＋ Tick 调度 ＋ 效果执行"，不再承担反应判定（§13 D8）。
    /// <para>纯 C#：不碰 <c>MonoBehaviour</c> / <c>Time</c> / <c>Physics2D</c>，EditMode 里喂两参就能直测。</para>
    /// </remarks>
    public sealed class TileElementReactor : IElementReactor
    {
        private readonly Dictionary<Vector3Int, ElementValue> _elements = new();

        private readonly IReadOnlyList<ElementRuleSpec> _rules;

        /// <param name="rules">元素反应规则（顺序即优先级）；为 <c>null</c> 或空时任何反应都不发生。</param>
        public TileElementReactor(IReadOnlyList<ElementRuleSpec> rules)
        {
            _rules = rules;
        }

        /// <summary>当前有元素记录的格数（诊断用；常规格不占常驻内存）。</summary>
        public int ElementCellCount => _elements.Count;

        /// <inheritdoc />
        public ElementValue GetElement(Vector3Int cell)
        {
            return _elements.TryGetValue(cell, out ElementValue value) ? value : default;
        }

        /// <inheritdoc />
        public void SetElement(Vector3Int cell, in ElementValue element)
        {
            if (element.IsEmpty)
            {
                _elements.Remove(cell);
                return;
            }

            _elements[cell] = element;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>判据只看"元素是不是空的"，不看"状态是不是 Normal"</b>：早先的写法把普通格一律当成"摘掉记录"，
        /// 于是<b>常规格表自己的元素永远刷不上去</b>（<c>RegisterCell</c> 想按表给地面一个"含土"的底也被吞了），
        /// 而那片地是什么脾性恰恰写在 <c>tile_state</c> 的 <c>Normal</c> 那一行里。
        /// 空元素才摘记录：留着全零的条目与"没有条目"提供的信息完全一样，只会让常驻内存悄悄长大。
        /// </remarks>
        public void FlushStateElement(Vector3Int cell, in TileStateSpec spec)
        {
            SetElement(cell, spec.Element);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 合成结果<b>写回该格</b>：它是"这一次落地之后这一格当下的元素"。若随后的规则没有真的命中（走兜底行、状态也不变），
        /// <c>GridLogic</c> 不会切状态、也就不会刷新初值 —— 所以这里在状态没变时必须自己收尾（见 <c>GridLogic.OnBallHit</c>）。
        /// </remarks>
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

        /// <summary>清空全部元素记录（切场景 / 打空重来时随格子一起复位）。</summary>
        public void Clear()
        {
            _elements.Clear();
        }
    }
}
