using UnityEngine;

namespace DeepseaOil.Logic.Movement
{
    /// <summary>
    /// 移动层的门禁：上层（状态效果层 / 战斗层）只能<b>提交请求</b>，由移动层决定怎么落到速度上。
    /// </summary>
    /// <remarks>
    /// <b>串行门禁的载体就是本结构</b>：状态效果层 → 战斗层 → 移动层，每层向下层输出一个门禁，
    /// 只有移动层拥有"写速度"的权限（不变量：角色速度只有一个写者）。
    /// <para><b>两条门禁的分工：</b>强制速度（受击滑行）<b>整条接管</b>本帧速度；
    /// 速度乘数（泥浆减速这类）只是一次缩放，方向仍由移动层自己的状态决定。
    /// 两者同时提交时<b>强制速度优先</b>：受击是外力，不该再被地面减速拖慢。</para>
    /// <para><b>为什么速度乘数落在"目标速度"上而不是乘在已提交的速度上：</b>
    /// 后者会与加速度互相拉锯 —— 每帧"加速一点、再整体乘 0.45"，稳态速度远低于
    /// <c>配置速度 × 乘数</c>（实测量级差 7 倍）。所以门禁只把乘数交给账本，
    /// 由状态算目标速度时带上它（见 <c>ActorLogic.SetSpeedScale</c>）。</para>
    /// <para>只读结构体：它每帧在栈上传递，不进堆。<b><c>default</c> 是合法的空门禁</b>
    /// （没有强制速度、乘数读作 1），所以 <see cref="None"/> 就是 <c>default</c>。</para>
    /// </remarks>
    public readonly struct MoveGates
    {
        /// <summary>是否要求移动层接管速度（受击 / 硬直）。</summary>
        public readonly bool HasForcedVelocity;

        /// <summary>要求移动层接管的速度（世界向量，单位/秒）。</summary>
        public readonly Vector2 ForcedVelocity;

        /// <summary>是否带了速度乘数（<c>false</c> 时 <see cref="SpeedScale"/> 读作 1）。</summary>
        public readonly bool HasSpeedScale;

        private readonly float _speedScale;

        private MoveGates(Vector2 forcedVelocity, bool hasForcedVelocity, bool hasSpeedScale, float speedScale)
        {
            ForcedVelocity = forcedVelocity;
            HasForcedVelocity = hasForcedVelocity;
            HasSpeedScale = hasSpeedScale;
            _speedScale = speedScale;
        }

        /// <summary>
        /// 速度乘数（<c>0..1</c>）；没带乘数时读作 <c>1</c>（不缩放）。
        /// </summary>
        /// <remarks>越界值夹到 <c>0..1</c>：门禁只表达"变慢"，加速是另一件事
        /// （它应当有自己的门禁与生产者，而不是让这里悄悄支持 &gt; 1）。</remarks>
        public float SpeedScale => HasSpeedScale
            ? (_speedScale <= 0f ? 0f : (_speedScale > 1f ? 1f : _speedScale))
            : 1f;

        /// <summary>受击：本帧速度由外力整条接管。</summary>
        /// <param name="forcedVelocity">本帧该被推成的速度（零 = 硬停）。</param>
        public static MoveGates Forced(Vector2 forcedVelocity)
        {
            return new MoveGates(forcedVelocity, true, false, 1f);
        }

        /// <summary>只缩放速度（泥浆减速这类），不接管方向。</summary>
        /// <param name="speedScale">速度乘数（<c>0..1</c>；<c>0</c> = 定住）。</param>
        public static MoveGates Scaled(float speedScale)
        {
            return new MoveGates(default, false, true, speedScale);
        }

        /// <summary>无门禁：移动层按自己的状态写速度。</summary>
        public static MoveGates None => default;
    }
}
