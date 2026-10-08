using UnityEngine;

namespace DeepseaOil.Data
{
    /// <remarks>运动参数是装配期 Configure 一次性折算的快照，事后改 SO 不生效</remarks>
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
