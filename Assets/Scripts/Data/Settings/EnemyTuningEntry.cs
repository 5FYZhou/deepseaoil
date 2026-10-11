using System;
using UnityEngine;

namespace DeepseaOil.Data
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

        [Tooltip("超出这个距离就放弃追击（世界单位）")]
        public float chaseRange = 60f;
    }
}
