using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Wave;
using UnityEngine;

namespace DeepseaOil.Presentation.Actor
{
    /// <summary>战斗调度：按波次生成敌人、逐只驱动、清场。<b>它是敌人的组合根（造 ＋ 持 ＋ 驱）</b>。</summary>
    /// <remarks>世界信息（玩家引用、波次数值、格子门面、归属表）全部经 <see cref="Initialize"/> 注入，本类不自己去找世界；计时与分支交给 <see cref="WaveLogic"/>（纯逻辑、可喂 dt 复现）。
    /// <b>不自己挂 <c>FixedUpdate</c></b>：由组合根在每个物理帧调 <see cref="FixedTick"/>，自驱会让帧内顺序不可预测。<b>它是存活数与波次的唯一权威</b>：HUD 读的 <c>WaveChanged</c> 由这里发布。</remarks>
    public sealed class CombatDirector : MonoBehaviour
    {
        private WaveLogic _logic;
        private EnemySpec _enemySpec;
        private Transform _player;
        private Transform _actorRoot;
        private GridLogic _grid;
        private EnemyCellRegistry _registry;

        private readonly List<EnemyActor> _enemies = new List<EnemyActor>();
        private readonly List<WaveLogic.SpawnRequest> _spawnBuffer = new List<WaveLogic.SpawnRequest>();

        private int _publishedWave = -1;
        private int _publishedAlive = -1;

        public int AliveCount { get; private set; }

        /// <summary>列表里<b>第一只存活敌人</b>离玩家多远；没有敌人时 <c>-1</c>（取列表中第一只存活的，<b>不比较距离</b>）。</summary>
        public float FirstAliveEnemyDistance { get; private set; } = -1f;

        /// <summary>列表里第一只存活敌人的引擎速度（单位/秒）；与 <see cref="FirstAliveEnemyDistance"/> 一起看才能分开两种"不动"（速度非零 = 撞墙 / 被顶住）。</summary>
        public Vector2 FirstAliveEnemyVelocity { get; private set; }

        /// <summary>组装调度器。</summary>
        /// <param name="player">玩家组合根；为 <c>null</c>（或它的逻辑层没装配好）时本组件停用。</param>
        public void Initialize(
            PlayerController player,
            in WaveSpec waveSpec,
            EnemySpec enemySpec,
            GridLogic grid,
            EnemyCellRegistry registry,
            Transform actorRoot)
        {
            if (player == null || player.Logic == null)
            {
                Debug.LogError("CombatDirector 没有玩家引用（或玩家逻辑层没装配好），敌人不会生成，已停用。", this);
                enabled = false;
                return;
            }

            _player = player.transform;
            _enemySpec = enemySpec;
            _grid = grid;
            _registry = registry;
            _actorRoot = actorRoot;
            _logic = new WaveLogic(in waveSpec);

            PublishIfChanged();
        }

        /// <summary>清空全场敌人并停掉当前波次（玩家被打空时调用：不清的话玩家一复活就会被原地的敌人立刻再打一次）。</summary>
        public void ClearAll()
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i] != null) Destroy(_enemies[i].gameObject);
            }

            _enemies.Clear();

            // _logic 可能为 null：player 未接线时 Initialize 会提前返回（那时也不会有敌人）
            _logic?.Reset();

            AliveCount = 0;
            FirstAliveEnemyDistance = -1f;
            FirstAliveEnemyVelocity = Vector2.zero;

            PublishIfChanged();

            // 不静默：白模的"敌人全没了"必须能追溯到一次清场，而不是"刷怪坏了"。
            Debug.Log("[Combat] 敌人清场，等待下一波");
        }

        /// <summary>推进一个物理帧：驱动敌人 → 刷读数 → 跑波次。</summary>
        /// <remarks>暂停时 <c>timeScale</c> 为 0、<c>deltaTime</c> 也是 0，计时与移动自然冻住；组合根仍然会挡一层：恢复那一帧不该补上一大段"暂停期间欠下的"生成量。</remarks>
        public void FixedTick(float now, float deltaTime)
        {
            if (_logic == null || _player == null) return;

            ClearDestroyed();

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || !enemy.IsAlive) continue;

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
            var go = new GameObject($"Enemy_{request.WaveIndex}_{request.Remaining}");

            if (_actorRoot != null) go.transform.SetParent(_actorRoot, false);

            var actor = go.AddComponent<EnemyActor>();

            actor.Initialize(
                request.Position,
                in _enemySpec,
                _player,
                PlayerPosition() - request.Position,
                _grid,
                _registry,
                _actorRoot);

            _enemies.Add(actor);
        }

        private void ClearDestroyed()
        {
            // 倒序删：正序删除会跳过紧挨着的下一个元素，而那种漏删不报错、只表现为"列表越来越长"。
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
                if (enemy != null && enemy.IsAlive) alive++;
            }

            return alive;
        }

        private void UpdateReadouts()
        {
            FirstAliveEnemyDistance = -1f;
            FirstAliveEnemyVelocity = Vector2.zero;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyActor enemy = _enemies[i];

                if (enemy == null || !enemy.IsAlive) continue;

                FirstAliveEnemyDistance = Vector2.Distance(enemy.Position, PlayerPosition());
                FirstAliveEnemyVelocity = enemy.EngineVelocity;

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
