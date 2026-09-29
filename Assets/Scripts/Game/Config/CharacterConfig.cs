using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace DeepSeaOil.Config
{
    public class CharacterConfig : BaseConfig
    {
        [Header("名称")] public string Name;
        [Header("Move")]
        [Tooltip("地面最大水平速度")]
        public float moveSpeed = 8f;

        [Tooltip("水平加速度")]
        public float moveAcceleration = 60f;

        [Tooltip("反向输入的转向衰减率（1/秒），越大转身越快；20 为首个猜测值，待调参")]
        public float turnDecayRate = 20f;

        [Tooltip("最大下落速度")]
        public float maxFallSpeed = 20f;

        [Tooltip("最大上升速度")]
        public float maxRiseSpeed = 20f;

        [Header("Jump")]
        [Tooltip("起跳初速度")]
        public float jumpSpeed = 14f;

        [Tooltip("二段跳初速度")]
        public float doubleJumpSpeed = 12f;

        [Tooltip("上升期且按住跳跃键时的重力加速度")]
        public float riseGravity = 40f;

        [Tooltip("其余情况的重力加速度")]
        public float fallGravity = 70f;

        [Tooltip("上升期松开跳跃键时的垂直速度截断系数")]
        [Range(0f, 1f)]
        public float jumpCutMultiplier = 0.5f;

        [Header("Dash")]
        [Tooltip("冲刺速度")]
        public float dashSpeed = 25f;

        [Tooltip("冲刺持续时长（秒）")]
        public float dashDuration = 0.2f;

        [Header("Wall")]
        [Tooltip("贴墙下滑速度")]
        public float wallSlideSpeed = 3f;
    }
}