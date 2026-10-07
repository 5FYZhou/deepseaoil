using UnityEngine;

namespace DeepseaOil.Presentation.Adapters
{
    /// <summary>
    /// 玩家移动执行器：<b>除了基类的三条共同物理参数之外，玩家侧没有额外要求</b>。
    /// </summary>
    /// <remarks>
    /// <b>它为什么存在（而不是让玩家继续用基类）：</b>基类是 <c>abstract</c>（玩家与敌人对称，
    /// 见 <see cref="ActorMotor"/> 的说明），所以玩家这一侧必须有一个具名子类。
    /// 它<b>刻意不写任何逻辑</b> —— 本批是"抽基类"而不是"顺手改玩家手感"，加进来的每一行都必须有需求。
    /// <para><b>它将来会长什么：</b>玩家独有的物理写法（插值方式、斜坡处理、被推挤时的质心）
    /// 都在这里，而不是回到基类去加开关 —— 基类只放"两个角色都要"的东西。</para>
    /// <para><b>改名会丢场景引用吗：</b>改名不会（Unity 按脚本资产的 GUID 绑定）；
    /// 但"从 <c>MovementMotor</c> 换成 <c>PlayerMotor</c>"是<b>换类型</b>，场景里现挂的那条会变成
    /// Missing Script，需要在 Inspector 里重挂一次。</para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMotor : ActorMotor
    {
    }
}
