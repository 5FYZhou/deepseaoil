using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>玩家移动执行器：读写、镜像与帧末唯一一次写速度都在 <see cref="ActorMotor"/>（引擎回读口与账本的差别见 <c>IActorMotor</c>）；玩家侧没有额外物理参数。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : ActorMotor
    {
    }
}
