using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using UnityEngine;

namespace DeepseaOil.Logic
{
    /// <summary>
    /// 敌人的运行时账本：<b>耐久 ＋ 存活态</b>。由表现侧的敌人实体持有。
    /// </summary>
    /// <remarks>
    /// <b>与玩家侧的 <c>PlayerStats</c> 同构</b>（审查已定"敌人也要一个 <c>EnemyStats</c> 包装"）：
    /// 只读配置从 <see cref="Spec"/> 出，可变状态从本类的字段出，生命体征走 <see cref="IAlivable"/>。
    /// 于是"这个敌人还剩多少血、死没死"只有一个答案，不再散在 <c>EnemyActor</c> 的私有字段里。
    /// <para><b>它不发事件</b>：敌人的耐久是表现层的即时读数（头顶数字当帧刷新），
    /// 没有任何跨层订阅者 —— 加一条没人听的事件只会让"谁在维护它"变成一个新的问题。</para>
    /// <para><b>取整而不是"有伤害就扣 1"</b>：将来出现 0.5 点伤害时行为才有意义。</para>
    /// </remarks>
    public sealed class EnemyStats : IAlivable
    {
        private readonly EnemySpec _spec;

        private bool _alive = true;

        /// <param name="spec">敌人取值边界（表行 ＋ 角色运动配置）。</param>
        public EnemyStats(EnemySpec spec)
        {
            _spec = spec;
            Hp = spec.Hp;
        }

        /// <summary>本局的配置取值边界（只读）：半径 / 耐久 / 闪烁频率 / 角色参数。</summary>
        public EnemySpec Spec => _spec;

        /// <summary>剩余耐久；归零即 <see cref="IsAlive"/> 为 <c>false</c>。</summary>
        public int Hp { get; private set; }

        /// <inheritdoc />
        public bool IsAlive => _alive;

        /// <summary>
        /// 扣一次耐久。
        /// </summary>
        /// <param name="amount">伤害值（浮点，配置口径）。</param>
        /// <remarks>
        /// <b>已死时是 no-op</b>（与 <c>EnemyActor.TakeDamage</c> 的旧守卫同义）：
        /// 死亡到销毁之间还隔着若干帧，那一段里的格结算不该再改一个尸体的耐久。
        /// <para>扣到 0 只翻转 <see cref="IsAlive"/>，<b>不做死亡表现</b> ——
        /// "判断死亡"与"做死亡表现"分开，才能让"死亡路径也要把状态写完整"这件事将来被复用。</para>
        /// </remarks>
        public void ApplyDamage(float amount)
        {
            if (!_alive || amount <= 0f) return;

            Hp = Mathf.Max(0, Hp - Mathf.RoundToInt(amount));

            if (Hp <= 0) _alive = false;
        }
    }
}
