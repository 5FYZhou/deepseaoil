using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>玩家专属参数，共用部分在 CharacterConfig</summary>
    /// <remarks>唯一读取口 ConfigModule.GetPlayer().Config，由 BindAssets 读一次</remarks>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "DeepseaOil/Settings/Player")]
    public class PlayerConfig : CharacterConfig
    {
        [Header("Input")]
        [Tooltip("输入缓冲容量（秒）：历史窗口时长，必须 ≥ 下面所有输入各自的窗口")]
        public float inputBufferTime = 0.12f;

        [Header("Dash")]
        [Tooltip("冲刺冷却（秒）")]
        public float dashCooldown = 1.5f;

        [Tooltip("冲刺输入缓冲窗口（秒）")]
        public float dashBufferTime = 0.12f;
    }
}
