using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    [CreateAssetMenu(fileName = "玩家配置", menuName = "角色/玩家")]
    public class PlayerConfig : CharacterConfig
    {
        [Header("Jump")]
        [Tooltip("两次落地之间可用的空中跳跃次数，1 表示可二段跳")]
        public int maxAirJumps = 1;

        [Tooltip("土狼时间（秒）：离地后仍可起跳的窗口")]
        public float coyoteTime = 0.1f;

        [Tooltip("跳跃输入缓冲窗口（秒）")]
        public float jumpBufferTime = 0.12f;

        [Header("Dash")]
        [Tooltip("两次落地之间可用的空中冲刺次数")]
        public int maxAirDashes = 1;

        [Tooltip("冲刺冷却（秒）")]
        public float dashCooldown = 1.5f;

        [Tooltip("冲刺输入缓冲窗口（秒）")]
        public float dashBufferTime = 0.12f;

        [Header("WallJump")]
        [Tooltip("蹬墙跳水平初速度")]
        public float wallJumpSpeedX = 8f;

        [Tooltip("蹬墙跳垂直初速度")]
        public float wallJumpSpeedY = 14f;

        [Tooltip("蹬墙跳后不响应水平输入的时长（秒）")]
        public float wallJumpLockTime = 0.2f;
    }
}
