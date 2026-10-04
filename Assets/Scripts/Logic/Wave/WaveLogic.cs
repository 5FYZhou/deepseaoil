using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;

namespace DeepseaOil.Logic.Wave
{
    /// <summary>
    /// 波次状态机：只管"何时刷、刷几只、刷在哪"，<b>不建物体</b>。
    /// </summary>
    /// <remarks>
    /// 从白模的 <c>EnemyDirector.UpdateSpawn</c> 整段搬出来，把"计时 + 分支"与
    /// "new GameObject + AddComponent + 维护存活列表"分开：前者是纯逻辑（可喂 dt 复现），
    /// 后者离不开引擎。
    /// <para><b>波次循环是刻意保留的</b>：它是"每波 4 只"这条配置唯一的验证方式 ——
    /// 只生成一次的话，"每波 4 只"与"一共 4 只"无法区分。</para>
    /// <para><b>它不感知伤害结算顺序。</b>"这一只死没死"由驱动方数存活数后喂进来
    /// （<paramref name="anyEnemyAlive"/>），状态机自己不认识敌人。</para>
    /// </remarks>
    public sealed class WaveLogic
    {
        /// <summary>一次生成请求。</summary>
        public readonly struct SpawnRequest
        {
            /// <summary>出生位置（世界坐标）。</summary>
            public readonly Vector2 Position;

            /// <summary>所属波次序号（从 1 起）。</summary>
            public readonly int WaveIndex;

            /// <summary>本波里这是第几只（递减，用于命名与日志）。</summary>
            public readonly int Remaining;

            public SpawnRequest(Vector2 position, int waveIndex, int remaining)
            {
                Position = position;
                WaveIndex = waveIndex;
                Remaining = remaining;
            }
        }

        private readonly WaveSpec _spec;

        /// <summary>当前波次序号，<b>从 1 起</b>。</summary>
        /// <remarks>
        /// 0 起会让"第几波"这个问题在 Console 里读起来别扭，而排查全靠 Console 对账。
        /// </remarks>
        private int _waveIndex;

        /// <summary>本波还剩几只没生成。</summary>
        private int _remaining;

        /// <summary>距离下一次动作的剩余时长（秒）。</summary>
        private float _timer;

        /// <summary>清场后到下一波之间的等待是否已在计时。</summary>
        private bool _waitingForNextWave;

        /// <summary>当前波次序号（从 1 起）。</summary>
        public int WaveIndex => _waveIndex;

        /// <summary>本波还剩几只没生成。</summary>
        public int Remaining => _remaining;

        /// <summary>本波是否还在陆续出生。</summary>
        public bool IsSpawning => _remaining > 0;

        /// <summary>是否正在"清完一波、等下一波"的间隙里。</summary>
        public bool IsWaitingForNextWave => _waitingForNextWave;

        public WaveLogic(in WaveSpec spec)
        {
            _spec = spec;

            Reset();
        }

        /// <summary>回到"开局"状态：第一波、按配置的初始延时。</summary>
        public void Reset()
        {
            _waveIndex = 1;
            _remaining = _spec.EnemiesPerWave;
            _timer = _spec.InitialDelay;
            _waitingForNextWave = false;
        }

        /// <summary>
        /// 推进一次；要生成的敌人追加到 <paramref name="output"/>。
        /// </summary>
        /// <param name="now">当前时间（秒）；出生点的角度与它有关，于是每波不会重叠。</param>
        /// <param name="dt">本帧时长（秒）；暂停时为 0，计时自然冻结。</param>
        /// <param name="anyEnemyAlive">场上是否还有活着的敌人（由驱动方数）。</param>
        /// <param name="playerPosition">玩家位置（出生环的圆心）。</param>
        /// <param name="output">输出列表，<b>会被先清空</b>。</param>
        public void Tick(
            float now,
            float dt,
            bool anyEnemyAlive,
            Vector2 playerPosition,
            List<SpawnRequest> output)
        {
            if (output == null) return;

            output.Clear();

            if (_remaining <= 0)
            {
                // 本波已全部生成：等它们死光，再隔一段时间开下一波。
                if (anyEnemyAlive)
                {
                    _waitingForNextWave = false;
                    return;
                }

                if (!_waitingForNextWave)
                {
                    _waitingForNextWave = true;
                    _timer = _spec.RespawnDelay;
                    return;
                }

                _timer -= dt;

                if (_timer > 0f) return;

                _remaining = _spec.EnemiesPerWave;
                _waitingForNextWave = false;

                // 波次序号只用于命名与日志：它让"这一只是第几波的"在 Console 里读得出来。
                // 不加它的话，重生的敌人与没死的老敌人长得一模一样，"清完又刷了"无从判断。
                _waveIndex++;
            }

            _timer -= dt;

            if (_timer > 0f) return;

            _remaining--;

            output.Add(new SpawnRequest(SpawnPosition(now, playerPosition), _waveIndex, _remaining));

            // 生成后必须重置计时：否则下一只会在一帧之后立刻跟着出，"一只一只出"就没了。
            // 本波出完之后这个值会在下一次 Tick 里被判空分支覆盖掉，所以不用特判。
            _timer = _spec.SpawnInterval;
        }

        /// <summary>
        /// 出生点：以玩家为圆心、按当前波次错开的一个环上。
        /// </summary>
        /// <remarks>
        /// 波次序号与剩余数都参与角度计算，所以第二波不会与第一波的出生点重合 ——
        /// 重合会让人以为是"上一波没死"，而它其实是新刷的。
        /// <para>错过的点不做重试：不判地形，越界由刚体撞墙兜住（与白模一致）。</para>
        /// </remarks>
        private Vector2 SpawnPosition(float now, Vector2 playerPosition)
        {
            float baseAngle = now * 0.7f + _waveIndex * 1.3f;

            float radians = baseAngle + _remaining * Mathf.PI * 0.5f;

            var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * _spec.SpawnRadius;

            return playerPosition + offset;
        }
    }
}
