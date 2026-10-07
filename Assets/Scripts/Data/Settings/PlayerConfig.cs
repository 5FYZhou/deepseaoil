using UnityEngine;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 玩家专属参数。移动的共用部分在 <see cref="CharacterConfig"/>。
    /// </summary>
    /// <remarks>
    /// 平台跳跃时代的 <c>coyoteTime</c>（土狼时间）/ <c>maxAirJumps</c>（空中跳跃余额）/
    /// <c>maxAirDashes</c> / <c>wallJumpSpeedX</c> / <c>wallJumpSpeedY</c> / <c>wallJumpLockTime</c>（蹬墙跳）
    /// 已随对应状态类一起删除。
    /// <c>inputBufferTime</c> 是<b>输入缓冲容量</b>（历史窗口时长），不专属于跳跃：
    /// <c>PlayerController</c> 用它决定 <c>InputBuffer</c> 容量，各输入各自的响应窗口仍由自己的参数给出
    /// （如 <c>dashBufferTime</c>）——容量必须 ≥ 所有窗口，否则窗口内的按下会被挤出历史。
    /// <para><b>资产位置与读取口：</b><c>Assets/Resources/config/PlayerConfig.asset</c>，
    /// 由 <c>ConfigModule.BindAssets</c> 走 <c>AssetModule</c> 读一次，
    /// 经 <c>ConfigModule.GetPlayer().Config</c> 交给消费者 —— 不再由 <c>PlayerController</c>
    /// 在 Inspector 上拖（那样"玩家参数从哪来"就有两个答案）。</para>
    /// </remarks>
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
