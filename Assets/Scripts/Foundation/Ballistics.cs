using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>抛物线几何，归一化弧高：<c>t</c> 是已飞比例（调用方负责夹到 0..1），两端恰好为 0、<c>t = 0.5</c> 恰好为 1。本函数不夹 <c>t</c>：越界那一帧由调用方结算（夹在这里会掩盖调用方越界，而那种掩盖不报错）。</summary>
    public static class Ballistics
    {
        public static float ArcHeight01(float t)
        {
            return 4f * t * (1f - t);
        }
    }
}
