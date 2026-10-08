using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>玩家移动执行器，无额外物理参数</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : ActorMotor
    {
    }
}
