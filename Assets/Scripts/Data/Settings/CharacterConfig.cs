using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 角色共用运动参数：速度、斜向规则、外力强度、冲刺。
    /// </summary>
    /// <remarks>
    /// 字段按"谁在用"分三组：
    /// ① 正在被消费：<c>moveSpeed</c> / <c>snapToEightDirections</c> / <c>dashSpeed</c> / <c>dashDuration</c>；
    /// ② 通用控制律：<c>moveAcceleration</c> / <c>turnDecayRate</c> —— 俯视角玩家用不上（零惯性），敌人追击与击退滑行要用，
    ///    由 <c>ActorLogic.ApproachX</c> 消费；<c>hurtDecay</c> 是受击滑停的减速度（填 0 落回 <c>moveAcceleration</c>），
    ///    玩家与敌人因此可以各填一份"被撞多远"；
    /// ③ 外力强度缩放：<c>extraForceScale</c> —— 由 <c>ActorLogic.ApplyExtraForce</c> 消费，玩家填 0（不施加外力），
    ///    结冰打滑、水流推挤、被吸附等按需填正数。
    /// 重力与跳跃曲线（<c>jumpSpeed</c> / <c>riseGravity</c> / <c>fallGravity</c> / <c>jumpCutMultiplier</c> /
    /// <c>maxFallSpeed</c> / <c>maxRiseSpeed</c>）已随平台跳跃品类一起删除：俯视角没有"上"这个方向，
    /// 其只属于玩家的那部分字段也没有任何消费者。外力取代了它们原来的位置。
    /// 删字段前先确认没有敌人侧消费者。
    /// </remarks>
    public class CharacterConfig : BaseConfig
    {
        [Header("名称")]
        public string Name;

        [Header("Move")]
        [Tooltip("移动速度（单位/秒）。俯视角零惯性：这是速度，不是加速度")]
        public float moveSpeed = 8f;

        [Tooltip("8 向吸附：把输入方向吸附到 45° 一档并归一化。勾选则斜向与直向同速；摇杆轻推的模拟幅度不受影响")]
        public bool snapToEightDirections = true;

        [Tooltip("水平加速度：通用控制律，俯视角玩家不用，敌人加速/击退滑行用")]
        public float moveAcceleration = 60f;

        [Tooltip("反向输入的转向衰减率（1/秒），越大转身越快；供需要惯性的角色使用")]
        public float turnDecayRate = 20f;

        [Tooltip("受击滑停的减速度（单位/秒²）。填 0 = 沿用 moveAcceleration；玩家与敌人的「被撞出去多远」因此可以分开调")]
        public float hurtDecay = 0f;

        [Header("Extra Force")]
        [Tooltip("外力累加强度缩放。俯视角玩家填 0（不施加外力）；需要被击退/被推/被吸附的角色填 1 或更大")]
        public float extraForceScale = 0f;

        [Header("Dash")]
        [Tooltip("冲刺速度")]
        public float dashSpeed = 25f;

        [Tooltip("冲刺持续时长（秒）")]
        public float dashDuration = 0.2f;
    }
}
