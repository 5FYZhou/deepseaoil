using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>会互相让开的同伴：软分离只认它，不认格子注册表里的测试假目标</summary>
    public interface IEnemyBody
    {
        Vector2 Position { get; }

        /// <summary>体半径（世界单位）</summary>
        float Radius { get; }
    }
}
