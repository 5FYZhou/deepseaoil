using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    // 锚点：不实现取 transform.position.y；View 会被抖动改写
    public interface IYSortAnchor
    {
        Vector2 SortAnchor { get; }
    }
}
