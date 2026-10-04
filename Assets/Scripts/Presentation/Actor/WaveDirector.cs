using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Wave;
using UnityEngine;

namespace DeepseaOil.Presentation.Actor
{
    /// <summary>
    /// 敌人调度：按波次生成、逐只驱动、清场。<b>它是敌人的组合根</b>。
    /// </summary>
    /// <remarks>
    /// 白模里"波次计时 + 建物体 + 存活计数 + 逐只驱动"是同一个类；这里把计时与分支交给
    /// <see cref="WaveLogic"/>（纯逻辑、可喂 dt 复现），本类只做三件引擎相关的事：
    /// 建物体、按固定顺序驱动、数存活数。
    /// <para><b>不自己挂 <c>FixedUpdate</c></b>：由组合根（<c>CombatRoot</c>）在每个物理帧调
    /// <see cref="FixedTick"/>。框架的硬契约是"每帧只有四个驱动入口"，自驱会让帧内顺序不可预测。</para>
    /// <para><b>它是存活数与波次的唯一权威</b>：HUD 读的 <c>WaveChanged</c> 由这里发布 ——
    /// 只有它同时知道"第几波"（来自 <see cref="WaveLogic"/>）与"还剩几只"（来自敌人列表）。</para>
    /// </remarks>
    public sealed class WaveDirector : MonoBehaviour
    {
        [Tooltip("敌人的父物体。留空则在场景根下建（只为层级整洁，不影响行为）。")]
        [SerializeField] private Transform actorRoot = default;

        private WaveLogic _logic;
        private EnemySpec _enemySpec;
        private Transform _player;
        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();
        private readonly List<WaveLogic.SpawnRequest> _spawnBuffer = new List<WaveLogic.SpawnRequest>();

        private int _publishedWave = -1;
        private int _publishedAlive = -1;

        /// <summary>场上存活敌人数。</summary>
        public int AliveCount { get; private set; }

        /// <summary>第一只存活敌人离玩家多远；没有敌人时 <c>-1</c>。</summary>
        /// <remarks>
        /// <b>这是"敌人在动吗"这个问题的直接读数。</b>连续看几帧：一直在变小 = 在追；不动 = 卡住了；
        /// 在变大 = 刚被击退。俯视角下几米外的小圆盘是看不出慢速位移的。
        /// </remarks>
        public float NearestEnemyDistance { get; private set; } = -1f;

        /// <summary>第一只存活敌人的引擎速度（单位/秒）。</summary>
        /// <remarks>与 <see cref="NearestEnemyDistance"/> 一起看才能分开两种"不动"：
        /// 速度非零但距离不变 ⇒ 它撞在墙或别的敌人上；速度为零 ⇒ 逻辑层真的没在驱动它。</remarks>
        public Vector2 NearestEnemyVelocity { get; private set; }

        /// <summary>
        /// 组装调度器。
        /// </summary>
        /// <param name="player">玩家。为 <c>null</c> 时本组件停用（不刷出不追人的敌人）。</param>
        /// <param name="waveSpec">波次数值。</param>
        /// <param name="enemySpec">敌人种类数值。</param>
        /// <param name="grid">格子门面。</param>
        /// <param name="registry">敌人归属表。</param>
        public void Initialize(
            Transform player,
            in WaveSpec waveSpec,
            in EnemySpec enemySpec,
            GridLogic grid,
            EnemyCellRegistry registry)
        {
            if (player == null)
            {
                Debug.LogError("WaveDirector 没有玩家引用，敌人不会生成，已停用。", this);
                enabled = false;
                return;
            }

            _player = player;
            _enemySpec = enemySpec;
            _grid = grid;
            _registry = registry;
            _logic = new WaveLogic(in waveSpec);

            PublishIfChanged();
        }

        /// <summary>
        /// 清空全场敌人并停掉当前波次。
        /// </summary>
        /// <remarks>
        /// 玩家被打空时调用：不清的话，玩家一复活就会被原地的敌人立刻再打一次
        /// （它们还站在原地，而玩家回到了出生点）。
        /// </remarks>
        public void ClearAll()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null) Destroy(_enemies[i].gameObject);
            }

            _enemies.Clear();

            _logic.Reset();

            AliveCount = 0;
            NearestEnemyDistance = -1f;
            NearestEnemyVelocity = Vector2.zero;

            PublishIfChanged();

            // 不静默：白模的"敌人全没了"必须能追溯到一次清场，而不是"刷怪坏了"。
            Debug.Log("[Combat] 敌人清场，等待下一波");
        }

        /// <summary>推进一个物理帧：驱动敌人 → 刷读数 → 跑波次。</summary>
        /// <param name="now">物理时间（<c>Time.fixedTime</c>）。</param>
        /// <param name="deltaTime">物理步长（<c>Time.fixedDeltaTime</c>）。</param>
        /// <remarks>
        /// 暂停时 <c>timeScale</c> 为 0、<c>deltaTime</c> 也是 0 —— 计时与移动自然冻住。
        /// 组合根仍然会挡一层：恢复那一帧不该补上一大段"暂停期间欠下的"生成量。
        /// </remarks>
        public void FixedTick(float now, float deltaTime)
        {
            if (_logic == null || _player == null) return;

            ClearDestroyed();

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || enemy.IsDead) continue;

                enemy.FixedTick(now, deltaTime);
            }

            AliveCount = CountAlive();
            UpdateReadouts();

            _logic.Tick(now, deltaTime, AliveCount > 0, PlayerPosition(), _spawnBuffer);

            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                SpawnOne(_spawnBuffer[i]);
            }

            PublishIfChanged();
        }

        private void SpawnOne(in WaveLogic.SpawnRequest request)
        {
            var go = new GameObject($"敌人_{request.WaveIndex}_{request.Remaining}");

            if (actorRoot != null) go.transform.SetParent(actorRoot, false);

            var actor = go.AddComponent<EnemyActor>();

            actor.Initialize(
                request.Position,
                in _enemySpec,
                _player,
                PlayerPosition() - request.Position,
                _grid,
                _registry,
                actorRoot);

            _enemies.Add(actor);
        }

        /// <summary>清掉已经销毁的引用，防止列表无限增长。</summary>
        /// <remarks>
        /// 用倒序删除：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
        /// </remarks>
        private void ClearDestroyed()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] == null) _enemies.RemoveAt(i);
            }
        }

        private int CountAlive()
        {
            int alive = 0;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                // 已销毁的对象在列表里还是非空引用，Unity 的 null 判定会挡住它们。
                if (enemy != null && !enemy.IsDead) alive++;
            }

            return alive;
        }

        private void UpdateReadouts()
        {
            NearestEnemyDistance = -1f;
            NearestEnemyVelocity = Vector2.zero;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || enemy.IsDead) continue;

                NearestEnemyDistance = Vector2.Distance(enemy.Position, PlayerPosition());
                NearestEnemyVelocity = enemy.EngineVelocity;

                return;
            }
        }

        /// <summary>只在"波次或存活数真的变了"时发布，避免每物理帧刷一条事件。</summary>
        private void PublishIfChanged()
        {
            int wave = _logic != null ? _logic.WaveIndex : 0;

            if (wave == _publishedWave && AliveCount == _publishedAlive) return;

            _publishedWave = wave;
            _publishedAlive = AliveCount;

            EventBus<WaveChanged>.Publish(new WaveChanged(wave, AliveCount));
        }

        /// <summary>把当前波次与存活数重播一次（HUD 面板加载完成时用）。</summary>
        public void Announce()
        {
            _publishedWave = -1;
            _publishedAlive = -1;

            PublishIfChanged();
        }

        private Vector2 PlayerPosition()
        {
            Vector3 p = _player.position;

            return new Vector2(p.x, p.y);
        }
    }
}
