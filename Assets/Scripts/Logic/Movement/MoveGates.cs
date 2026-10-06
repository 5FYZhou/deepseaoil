using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 移动层的门禁：上层（状态效果层 / 战斗层）只能<b>提交请求</b>，由移动层决定怎么落到速度上。
    /// </summary>
    /// <remarks>
    /// <b>串行门禁的载体就是本结构</b>：状态效果层 → 战斗层 → 移动层，每层向下层输出一个门禁，
    /// 只有移动层拥有"写速度"的权限（不变量：角色速度只有一个写者）。
    /// <para><b>为什么现在只有"强制速度"一项：</b>加字段的原则是"有人生产它"。
    /// 受击（击退滑行）会生产强制速度；"速度缩放"（泥浆减速这一类）在格子那批接上时再加 ——
    /// 现在加进来就是一个永远没人写的字段，而没人写的字段最容易在下一次改动里被误当成"已实现"。</para>
    /// <para>只读结构体：它每帧在栈上传递，不进堆。</para>
    /// </remarks>
    public readonly struct MoveGates
    {
        /// <summary>是否要求移动层接管速度（受击 / 硬直）。</summary>
        public readonly bool HasForcedVelocity;

        /// <summary>要求移动层接管的速度（世界向量，单位/秒）。</summary>
        public readonly Vector2 ForcedVelocity;

        public MoveGates(Vector2 forcedVelocity)
        {
            HasForcedVelocity = true;
            ForcedVelocity = forcedVelocity;
        }

        /// <summary>无门禁：移动层按自己的状态写速度。</summary>
        public static MoveGates None => default;
    }
}
