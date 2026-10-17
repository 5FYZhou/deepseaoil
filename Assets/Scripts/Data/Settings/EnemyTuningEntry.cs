using System;
using UnityEngine;

namespace DeepseaOil.Data.Settings
{
        // 单个怪种的运学调参：enemy.id 一行，怪与怪不共用
        // 预制体碰撞体半径必须与本条 radius 一致，不一致不报错
    [Serializable]
    public sealed class EnemyTuningEntry
    {
        [Tooltip("对应 enemy 表的 id")]
        public int id = 1;

        [Tooltip("判定半径：Gizmo 圈与预制体碰撞体半径都读它")]
        public float radius = 0.45f;

        [Tooltip("加速度（单位/秒²）：起步与击退滑停的斜率")]
        public float acceleration = 14f;

        [Tooltip("击退衰减率（1/秒）：同时喂转向衰减与受击滑停")]
        public float knockbackDecay = 10f;

        [Tooltip("贴到这个距离就停住，不再压上（世界单位）")]
        public float stopDistance = 0.6f;

        [Tooltip("转向闸门的角速率上限（度/秒）：期望方向掉头时命令每秒最多转这么多度。填 0 = 关闭闸门")]
        public float turnRateLimit = 540f;

        [Tooltip("转向闸门的低通时间常数（秒）：越大越钝。40~60 毫秒够把每帧翻面压成划弧。填 0 = 关闭闸门")]
        public float turnSmoothSeconds = 0.05f;

        [Tooltip("已退役：追逐没有距离上限了，改这个字段不会有反应；仅为旧资产序列化保留")]
        public float chaseRange = 60f;

        [Tooltip("已退役：仇恨系统已下线，改这个字段不会有反应；仅为旧资产序列化保留")]
        public float visionRange = 0f;

        [Tooltip("已退役：仇恨系统已下线，改这个字段不会有反应；仅为旧资产序列化保留")]
        public float sustainRange = 0f;

        [Tooltip("已退役：仇恨系统已下线，改这个字段不会有反应；仅为旧资产序列化保留")]
        public float loseDelay = 0f;

        [Tooltip("受击形变：一怪一套，改这里即改观感，不用碰代码")]
        public SquashTuning squash = new SquashTuning();
    }

    // 受击形变调参：形变量是 localScale 的增量比例，不是像素
    [Serializable]
    public sealed class SquashTuning
    {
        [Tooltip("强度 0~1：0.18 = 垂直拉高 18%。0 即关闭该怪种的形变")]
        public float amount = 0.18f;

        [Tooltip("普通受击时长（秒）")]
        public float normalDuration = 0.1f;

        [Tooltip("致死一击时长（秒）：比普通长一点，看得见")]
        public float deathDuration = 0.14f;

        [Tooltip("X 压缩量相对 Y 拉伸量的比例：0.6 = 只压六成")]
        public float compressRatio = 0.6f;

        [Tooltip("回弹曲线指数：1 = 线性，2 = 二次缓出（先快后慢），越大回弹越脆")]
        public float easePower = 2f;

        [Tooltip("强度硬上限：手滑把 amount 配成 3.0 会把角色拉成针")]
        public float maxAmount = 0.5f;
    }
}
