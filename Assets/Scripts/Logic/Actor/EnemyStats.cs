using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using UnityEngine;

namespace DeepseaOil.Logic
{
    public sealed class EnemyStats : IAlivable
    {
        private readonly EnemySpec _spec;

        private bool _alive = true;

        public EnemyStats(EnemySpec spec)
        {
            _spec = spec;
            Hp = spec.Hp;
        }

        public EnemySpec Spec => _spec;

        public int Hp { get; private set; }

        public bool IsAlive => _alive;

        /// <summary>
        /// 扣一次耐久：浮点 amount 按 <c>Mathf.RoundToInt</c> 取整后从整数 Hp 扣；已死或 amount &lt;= 0 是 no-op。
        /// 扣到 0 只翻转 <see cref="IsAlive"/>，不做死亡表现（死亡到销毁之间还有若干帧，那段的格结算不再改尸体）。
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (!_alive || amount <= 0f) return;

            Hp = Mathf.Max(0, Hp - Mathf.RoundToInt(amount));

            if (Hp <= 0) _alive = false;
        }
    }
}
