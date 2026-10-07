
using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    /// <summary>
    /// 可以接受格子效果的目标。
    /// </summary>
    public interface IEffectTarget
    {
        bool IsDead { get; }

        Vector2 Position { get; }
    }
}
