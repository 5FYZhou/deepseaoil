using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>Y-Sort：把"世界 y 坐标"折成"渲染档位"的纯函数。只算数，频带的具体数值（起点 / 终点 / 每单位档数）由表现层的 <c>RenderOrder</c> 给。</summary>
    /// <remarks>方向：y 越小 ⇒ 档位越大 ⇒ 越晚绘制（<c>sortingOrder</c> 越大越晚画）。档位是量化的：同一档内顺序不确定；频带之外钳在两端，极远 / 极近退化为固定档位。</remarks>
    public static class YSort
    {
        /// <summary>世界 y → <c>[bandStart, bandEnd]</c> 内的档位（<c>bandStart</c> = 数值小的一端 ＝ 最远；两者颠倒时内部先交换）。取整口径 <c>floor(y × levelsPerUnit + 0.5)</c>，不是 <c>Mathf.RoundToInt</c>（银行家舍入会半档不换格）。<c>y</c> / <c>levelsPerUnit</c> 为 NaN 或 <c>levelsPerUnit ≤ 0</c> 时一律返回 <c>bandStart</c>（按最远处理，不报错）。</summary>
        public static int OrderFor(float y, int bandStart, int bandEnd, float levelsPerUnit)
        {
            if (bandEnd < bandStart)
            {
                int swap = bandStart;
                bandStart = bandEnd;
                bandEnd = swap;
            }

            if (float.IsNaN(y) || float.IsNaN(levelsPerUnit) || levelsPerUnit <= 0f) return bandStart;

            int stepped = Mathf.FloorToInt(y * levelsPerUnit + 0.5f);

            int order = bandEnd - stepped;

            if (order < bandStart) return bandStart;

            return order > bandEnd ? bandEnd : order;
        }
    }
}
