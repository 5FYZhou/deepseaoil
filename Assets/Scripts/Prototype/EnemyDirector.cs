using System.Collections.Generic;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Prototype
{
    /// <summary>
    /// 敌人调度：在玩家周围生成、逐帧驱动、清场重来。
    /// </summary>
    /// <remarks>
    /// <b>它和 <see cref="ThrowSpawner"/> 一样是"组合根"，由 <c>ThrowSpawner.Start</c> 建出来</b>，
    /// 所以场景里不需要多挂任何组件 —— 白模的铁律是"只挂一个"（忘了接线是白模绝大多数失败模式的来源）。
    /// <para><b>波次循环是刻意做的</b>：它是"每波敌人数 = <see cref="ThrowConstants.ENEMY_COUNT_PER_WAVE"/>"
    /// 这条常量唯一的验证方式。只生成一次的话，"每波 4 只"与"一共 4 只"在白模里无法区分，
    /// 而两者在真游戏里是完全不同的东西。</para>
    /// <para><b>它不参与伤害结算的判定顺序。</b>"同一颗球只打一次"由 <c>Damage.BallId</c> 判定，
    /// 与被谁先驱动无关 —— 曾经用过"每帧复位一个标记"的写法，那种写法把正确性绑死在
    /// 脚本执行顺序上（<c>ThrowSpawner.FixedUpdate</c> 与本类的 <c>FixedUpdate</c> 谁先跑 Unity 不保证）。</para>
    /// </remarks>
    public sealed class EnemyDirector : MonoBehaviour
    {
        private Transform _player;
        private EnemyConfig _config;
        private bool _paused;

        /// <summary>已生成的敌人（含正在被销毁的）。只在生成与清理时改动。</summary>
        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();

        /// <summary>本波还剩几只没生成。</summary>
        private int _waveRemaining;

        /// <summary>当前波次序号，从 1 起（只用于命名与日志）。</summary>
        private int _waveIndex;

        /// <summary>距离下一次生成的剩余时长（秒）。</summary>
        private float _spawnIn;

        /// <summary>清场后到下一波之间的等待是否已在计时。</summary>
        private bool _waitingForNextWave;

        /// <summary>场上还活着的敌人数。</summary>
        public int ActiveCount
        {
            get
            {
                int alive = 0;

                for (int i = 0; i < _enemies.Count; i++)
                {
                    EnemyActor enemy = _enemies[i];

                    // 已销毁的对象在列表里还是非空的引用，Unity 的 null 判定会挡住它们。
                    if (enemy != null && !enemy.IsDead) alive++;
                }

                return alive;
            }
        }

        /// <summary>
        /// 第一只存活敌人离玩家多远；没有敌人时返回 <c>-1</c>。
        /// </summary>
        /// <remarks>
        /// <b>这是"敌人在动吗"这个问题的直接读数。</b>连续看几帧这个数：
        /// 一直在变小 = 在追；不动 = 卡住了；在变大 = 刚被击退。
        /// <para>白模里所有"敌人看着不动"的报告都该先看这一个数 ——
        /// 否则只能靠盯着屏幕猜，而俯视角下几米外的小圆盘是看不出慢速位移的。</para>
        /// </remarks>
        public float NearestEnemyDistance { get; private set; } = -1f;

        /// <summary>第一只存活敌人的引擎速度（单位/秒）；没有敌人时为 <see cref="Vector2.zero"/>。</summary>
        /// <remarks>
        /// <b>与 <see cref="NearestEnemyDistance"/> 一起看才能分开两种"不动"：</b>
        /// 速度非零但距离不变 ⇒ 它撞在墙或别的敌人上；
        /// 速度为零 ⇒ 逻辑层真的没在驱动它。
        /// </remarks>
        public Vector2 NearestEnemyVelocity { get; private set; }

        /// <summary>刷新上面两个读数。由 <see cref="FixedUpdate"/> 在驱动完敌人之后调用。</summary>
        private void UpdateReadouts()
        {
            NearestEnemyDistance = -1f;
            NearestEnemyVelocity = Vector2.zero;

            if (_player == null) return;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || enemy.IsDead) continue;

                NearestEnemyDistance = Vector2.Distance(enemy.transform.position, PlayerPosition());
                NearestEnemyVelocity = enemy.CurrentVelocity;

                return;
            }
        }

        /// <summary>
        /// 组装调度器。
        /// </summary>
        /// <param name="player">玩家。为 <c>null</c> 时本组件停用（不刷出不追人的敌人）。</param>
        /// <param name="config">敌人数值。</param>
        /// <param name="initialDelay">开局到第一波之间的等待（秒）。</param>
        public void Initialize(Transform player, EnemyConfig config, float initialDelay)
        {
            if (player == null)
            {
                Debug.LogError("EnemyDirector 没有玩家引用，敌人不会生成，已停用。", this);
                enabled = false;
                return;
            }

            _player = player;
            _config = config;
            _spawnIn = initialDelay;
            _waveRemaining = ThrowConstants.ENEMY_COUNT_PER_WAVE;

            // 波次从 1 起：场景里第一波敌人叫"敌人_1_x"，而不是"敌人_0_x" ——
            // 0 起会让"第几波"这个问题在 Console 里读起来别扭，而白模全靠 Console 对账。
            _waveIndex = 1;

            EventBus<GamePaused>.Subscribe(OnPaused);
            EventBus<GameResumed>.Subscribe(OnResumed);
        }

        /// <summary>
        /// 清空全场敌人并停掉当前波次。
        /// </summary>
        /// <remarks>
        /// 玩家被打空时调用：不清的话，玩家一复活就会被原地的敌人立刻再打一次
        /// （它们还站在原地，而玩家回到了出生点 —— 结果可能是"刚复活就又被围住"）。
        /// </remarks>
        public void ClearAll()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null) Destroy(_enemies[i].gameObject);
            }

            _enemies.Clear();

            _waveRemaining = ThrowConstants.ENEMY_COUNT_PER_WAVE;
            _spawnIn = ThrowConstants.CHASE_INITIAL_DELAY;
            _waitingForNextWave = false;

            // Debug.Log 而不是静默：白模的"敌人全没了"必须能追溯到一次清场，而不是"刷怪坏了"。
            Debug.Log("[白模] 敌人清场，等待下一波");
        }

        private void OnDestroy()
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

        private void FixedUpdate()
        {
            // 暂停时 timeScale 为 0，Time.fixedDeltaTime 也是 0 —— 计时与移动自然冻住。
            // 这里仍然显式挡一层：恢复那一帧不该补上一大段"暂停期间欠下的"生成量。
            if (_paused || _player == null) return;

            float now = Time.fixedTime;
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || enemy.IsDead) continue;

                enemy.Tick(now, dt);
            }

            UpdateReadouts();

            UpdateSpawn(dt);
        }

        /// <summary>
        /// 同一波内两只敌人之间的间隔（秒）。
        /// </summary>
        /// <remarks>
        /// 一只一只出而不是一次全出：这样"每波几只"这件事在屏幕上数得清。
        /// 一次全出的话四只会叠在一起，看起来像两只。
        /// </remarks>
        private const float SpawnInterval = 0.25f;

        /// <param name="dt">本物理帧的时长。</param>
        private void UpdateSpawn(float dt)
        {
            if (_waveRemaining <= 0)
            {
                // 本波已全部生成：等它们死光，然后隔一段时间开下一波。
                // 用 ActiveCount 而不是"列表里还有没有元素"：已销毁的对象还留在列表里。
                if (ActiveCount > 0)
                {
                    _waitingForNextWave = false;
                    return;
                }

                if (!_waitingForNextWave)
                {
                    _waitingForNextWave = true;
                    _spawnIn = ThrowConstants.CHASE_RESPAWN_DELAY;

                    Debug.Log($"[白模] 第 {_waveIndex} 波清空，{ThrowConstants.CHASE_RESPAWN_DELAY:F1}s 后下一波");

                    return;
                }

                _spawnIn -= dt;

                if (_spawnIn > 0f) return;

                _waveRemaining = ThrowConstants.ENEMY_COUNT_PER_WAVE;
                _waitingForNextWave = false;

                // 波次序号只用于命名与日志：它让"这一只是第几波的"在 Console 里读得出来。
                // 不加它的话，重生的敌人与没死的老敌人长得一模一样，"清完又刷了"这件事无从判断。
                _waveIndex++;
            }

            _spawnIn -= dt;

            if (_spawnIn > 0f) return;

            SpawnOne();

            // 生成后必须重置计时：否则下一只会在一帧之后立刻跟着出，"一只一只出"就没了。
            // 本波出完之后这个值会在下一次 UpdateSpawn 里被判空分支覆盖掉，所以不用特判。
            _spawnIn = SpawnInterval;
        }

        private void SpawnOne()
        {
            _waveRemaining--;

            Vector2 position = SpawnPosition();

            var go = new GameObject($"敌人_{_waveIndex}_{_waveRemaining}");

            // 放在本组件之下：白模不往场景根目录丢垃圾，"敌人"在层级里能一眼收起来。
            go.transform.SetParent(transform, false);

            var actor = go.AddComponent<EnemyActor>();

            actor.Initialize(position, _config, _player, PlayerPosition() - position);

            _enemies.Add(actor);

            Debug.Log($"[白模] 生成敌人 #{_waveIndex}-{_waveRemaining}，位置 ({position.x:F2}, {position.y:F2})");
        }

        /// <summary>
        /// 出生点：以玩家为圆心、按当前波次错开的一个环上。
        /// </summary>
        /// <remarks>
        /// 波次序号参与角度计算，所以第二波不会与第一波的出生点重合 ——
        /// 重合会让人以为是"上一波没死"，而它其实是新刷的。
        /// <para>错过的点不做重试：白模不判地形（地图是空的），而地图边界由刚体撞墙兜住。</para>
        /// </remarks>
        private Vector2 SpawnPosition()
        {
            float baseAngle = Time.time * 0.7f + _waveIndex * 1.3f;

            float radians = baseAngle + _waveRemaining * Mathf.PI * 0.5f;

            Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ThrowConstants.CHASE_SPAWN_RADIUS;

            return PlayerPosition() + offset;
        }

        private Vector2 PlayerPosition()
        {
            Vector3 p = _player.position;

            return new Vector2(p.x, p.y);
        }
    }
}
