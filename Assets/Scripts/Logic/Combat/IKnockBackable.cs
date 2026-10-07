using UnityEngine;

namespace DeepseaOil.Logic.Combat
{
    public interface IKnockBackable
    {
        void ApplyKnockback(Vector2 impulse);
    }
}