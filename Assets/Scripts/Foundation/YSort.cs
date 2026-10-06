using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>
    /// Y-Sort：把"世界 y 坐标"折成"渲染档位"的<b>纯函数</b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么要有它：</b>俯视角下"谁挡住谁"应当由 y 决定（越靠下 ＝ 越靠近镜头 ＝ 越晚画）。
    /// 每个视效件各写一遍"y × 系数"的后果是它们迟早用不同的系数，而那种不一致看起来只是"偶尔穿模"。
    /// <para><b>方向约定：y 越小 ⇒ 档位越大 ⇒ 越晚绘制 ⇒ 盖在上面。</b>
    /// Unity 里 <c>sortingOrder</c> 越大越晚画，所以"更靠下"必须映射到"更大的档位"。</para>
    /// <para><b>档位是量化的</b>（每世界单位若干档）：同一档内的两个物体顺序不确定（由绘制/实例顺序决定）。
    /// 想要更细就调大 <c>levelsPerUnit</c>，但档数受频带宽度限制：频带之外的部分被钳在边界上，
    /// 于是"极远处"与"极近处"退化为频带两端的固定档位 —— 它们本来也不互相遮挡。</para>
    /// <para><b>它只算数</b>：频带的具体数值（起点/终点/每单位档数）由表现层的 <c>RenderOrder</c> 给 ——
    /// 数学在地基，约定在表现层。</para>
    /// </remarks>
    public static class YSort
    {
        /// <summary>把世界 y 折成频带内的档位。</summary>
        /// <param name="y">世界 y 坐标。</param>
        /// <param name="bandStart">频带下沿（数值小的那一端，最远）。</param>
        /// <param name="bandEnd">频带上沿（数值大的那一端，最近）。</param>
        /// <param name="levelsPerUnit">每世界单位几档；非法值（<c>≤ 0</c> / 非数）按下沿处理。</param>
        /// <returns>落在 <c>[bandStart, bandEnd]</c> 内的档位。</returns>
        /// <remarks>
        /// <b>非数按"最远"处理</b>：NaN 参与比较恒为 false，直接拿去算档位会得到一个不可预测的整数，
        /// 而那种对象会随机盖住别人 —— 宁可让它待在频带最底下。
        /// </remarks>
        public static int OrderFor(float y, int bandStart, int bandEnd, float levelsPerUnit)
        {
            if (bandEnd < bandStart)
            {
                int swap = bandStart;
                bandStart = bandEnd;
                bandEnd = swap;
            }

            if (float.IsNaN(y) || float.IsNaN(levelsPerUnit) || levelsPerUnit <= 0f) return bandStart;

            // 四舍五入用 floor(x + 0.5) 而不是 Mathf.RoundToInt：后者是"银行家舍入"（0.5 → 0、1.5 → 2），
            // 在档位这种"每半档就该换一格"的场合会得出不直观的结果。
            int stepped = Mathf.FloorToInt(y * levelsPerUnit + 0.5f);

            int order = bandEnd - stepped;

            if (order < bandStart) return bandStart;

            return order > bandEnd ? bandEnd : order;
        }
    }
}
