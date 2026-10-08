using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>归一化抛物线弧高：t 为已飞比例，两端为 0、t=0.5 为 1；本函数不夹 t，越界由调用方结算</summary>
    public static class Ballistics
    {
        public static float ArcHeight01(float t)
        {
            return 4f * t * (1f - t);
        }
    }
}
