using UnityEngine;

namespace DeepseaOil.Foundation
{
    /// <summary>唯一随机来源，纯函数式；Value01=[0,1)、InsideUnitCircle 模长小于1、Range=[min,max)</summary>
    public interface IRng
    {
        float Value01();

        Vector2 InsideUnitCircle();

        int Range(int minInclusive, int maxExclusive);
    }
}
