using DeepseaOil.Data;
using DeepseaOil.Logic.Combat;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Player;
using DeepseaOil.Presentation.Actor;
using UnityEngine;

namespace DeepseaOil.Presentation.Player
{
    /// <summary>
    /// 玩家受击：接触检测、无敌帧、被打退、打空重来。<b>血量状态在 <c>Logic.Player.PlayerHealth</c></b>。
    /// </summary>
    /// <remarks>
    /// <b>为什么血量不在本类：</b>逻辑层的硬约束是"零引擎类型"，而本类每一条都离不开引擎事实
    /// （接触检测、<c>Time.fixedTime</c>、刚体位置）。拆开之后，"无敌帧判据"可以在 EditMode 里喂时间戳断言。
    /// <para><b>本类不写刚体速度。</b>击退走 <c>PlayerController.OverrideLogicSpeedTargets</c> ＋
    /// <c>PlayerLogic.AddImpulse</c>，于是"玩家速度只有一个写者"这条不变量没被打破。</para>
    /// <para><b>接触伤害靠无敌帧挡，不靠"离开再回来"。</b>玩家贴在敌人身上时每一物理帧都会被查到，
    /// 没有无敌帧就是每帧扣一次、十帧打空 —— 那不是"被打了十下"，是一瞬间死。</para>
    /// </remarks>
    public sealed class PlayerHealthController : MonoBehaviour
    {
        /// <summary>接触检测复用的缓冲，避免每帧分配。</summary>
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        private PlayerController _player;
        private PlayerLogic _logic;
        private PlayerHealth _health;
        private PlayerSpec _spec;
        private WaveDirector _waveDirector;

        private Vector2 _spawnPoint;
        private float _retryAt = float.NegativeInfinity;
        private bool _paused;

        /// <summary>血量状态（只读，供调试读数与 HUD 重播）。</summary>
        public PlayerHealth Health => _health;

        /// <summary>
        /// 装配。
        /// </summary>
        /// <param name="player">玩家组合根（受击推挤要它的逻辑层入口）。</param>
        /// <param name="spec">玩家表值。</param>
        /// <param name="waveDirector">敌人调度器；打空时清场。可为 <c>null</c>。</param>
        public void Initialize(PlayerController player, in PlayerSpec spec, WaveDirector waveDirector)
        {
            if (player == null || player.Logic == null)
            {
                Debug.LogError("PlayerHealthController 没有拿到玩家的逻辑层，血量不会生效，已停用。", this);
                enabled = false;
                return;
            }

            _player = player;
            _logic = player.Logic;
            _spec = spec;
            _waveDirector = waveDirector;
            _spawnPoint = player.transform.position;
            _health = new PlayerHealth(in spec);
        }

        private void OnEnable()
        {
            EventBus<GamePaused>.Subscribe(OnPaused);
            EventBus<GameResumed>.Subscribe(OnResumed);
        }

        private void OnDisable()
        {
            EventBus<GamePaused>.Unsubscribe(OnPaused);
            EventBus<GameResumed>.Unsubscribe(OnResumed);
        }

        private void OnPaused(GamePaused evt)
        {
            _paused = true;
        }

        private void OnResumed(GameResumed evt)
        {
            _paused = false;
        }

        /// <summary>推进一个物理帧。</summary>
        /// <param name="now">物理时间（<c>Time.fixedTime</c>）。</param>
        public void FixedTick(float now)
        {
            if (_paused || _health == null) return;

            // 已经打空：等重试延时，然后回到出生点重来。
            if (!_health.IsAlive)
            {
                if (now >= _retryAt) ResetToSpawn();

                return;
            }

            if (TouchEnemy()) ApplyDamage(_spec.ContactDamage, now);
        }

        /// <summary>把当前血量重播一次（HUD 面板加载完成时用）。</summary>
        public void Announce()
        {
            _health?.Announce();
        }

        /// <summary>
        /// 扣血。<b>唯一入口</b>（接触、将来的陷阱与技能都走这里）。
        /// </summary>
        /// <param name="amount">伤害值；非正数直接忽略。</param>
        /// <param name="now">当前时间。</param>
        /// <returns>真的扣掉了血为 <c>true</c>。</returns>
        /// <remarks>
        /// <b>被挡掉时不扣血、不推、不写无敌时间</b>：三件事必须一起发生或一起不发生。
        /// 只扣血不推，玩家会被粘在敌人身上连扣；只推不写无敌，下一帧立刻再扣一次。
        /// </remarks>
        public bool ApplyDamage(float amount, float now)
        {
            if (_health == null) return false;

            if (!_health.ApplyDamage(amount, now)) return false;

            Knockback();

            if (!_health.IsAlive) _retryAt = now + _spec.RetryDelay;

            return true;
        }

        /// <summary>
        /// 回到出生点并满血。
        /// </summary>
        /// <remarks>
        /// 不做失败结算与开局 UI：一个能反复试的白模比一个"死了就停住"的白模有用得多。
        /// <para>清场是必须的：不清的话复活瞬间就贴着原来的敌人，下一次接触在 0.8 秒后又发生一次。</para>
        /// </remarks>
        private void ResetToSpawn()
        {
            _health.ResetToFull();

            _logic.StopMove();

            Vector2 position = _spawnPoint;

            // 边界钳位：出生点理论上在图内，但它是运行期读到的玩家位置；
            // 越界时钳回来，否则玩家会回到一张"看不见自己"的地图外。
            if (_player.World.Bounds.TryClamp(position, out Vector2 clamped)) position = clamped;

            _logic.ResetTo(position);

            if (_waveDirector != null) _waveDirector.ClearAll();
        }

        /// <summary>
        /// 本帧是否有敌人贴在玩家身上。
        /// </summary>
        /// <remarks>
        /// <b>判定用圆心距，不用接触点与法线：</b>玩家与敌人都只有一个碰撞体，圆心距的结论与逐点接触一致，
        /// 而圆心距能用 EditMode 测试直接喂坐标。
        /// <para>接触半径是"标签"而不是 <c>collider.Distance</c>：后者会随两个碰撞体的实际穿透量浮动，
        /// 于是"贴住了"的判定会随物理求解精度变化，不能测。</para>
        /// </remarks>
        private bool TouchEnemy()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _spec.ContactRadius,
                _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                EnemyActor enemy = _overlapBuffer[i].GetComponentInParent<EnemyActor>();

                if (enemy == null || enemy.IsDead) continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 把玩家推开一次。
        /// </summary>
        /// <remarks>
        /// <b>顺序不能反：先声明本帧上限、再累加冲量。</b>
        /// 上限压的是帧末那一次写出，冲量是往账本里加量 —— 反过来的话冲量这一帧不会被压住，
        /// 连着挨打就会越推越快（而"越推越快"看起来只是"手感飘"，不会报错）。
        /// </remarks>
        private void Knockback()
        {
            Vector2 away = AwayFromNearestEnemy();

            _player.OverrideLogicSpeedTargets(_logic, _spec.KnockbackSpeedLimit);
            _player.AddLogicImpulse(_logic, away * _spec.KnockbackImpulse);
        }

        /// <summary>从最近的敌人指向玩家的单位方向；没有敌人时给"下"。</summary>
        private Vector2 AwayFromNearestEnemy()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _spec.ContactRadius,
                _overlapBuffer);

            var self = new Vector2(transform.position.x, transform.position.y);

            Vector2 best = Vector2.down;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                EnemyActor enemy = _overlapBuffer[i].GetComponentInParent<EnemyActor>();

                if (enemy == null || enemy.IsDead) continue;

                Vector2 delta = self - enemy.Position;
                float distance = delta.sqrMagnitude;

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = distance > 1e-6f ? delta.normalized : Vector2.down;
            }

            return best;
        }
    }
}
