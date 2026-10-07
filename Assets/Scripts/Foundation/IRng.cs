using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>随机数端口：全工程唯一被允许的随机来源；实现须纯函数式（只依赖内部状态，不许读时间、不许读场景）。值域：<c>Value01</c> = [0, 1)、<c>InsideUnitCircle</c> 模长 &lt; 1、<c>Range</c> = [minInclusive, maxExclusive)。</summary>
    public interface IRng
    {
        float Value01();

        Vector2 InsideUnitCircle();

        int Range(int minInclusive, int maxExclusive);
    }
}
