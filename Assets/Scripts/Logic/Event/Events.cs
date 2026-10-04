using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using cfg.demo;

namespace DeepseaOil.Logic.Events
{
    // Intent
    public readonly struct RequestPause { }

    public readonly struct RequestResume { }

    public readonly struct RequestChangeScene 
    {
        public readonly string sceneName;
        public RequestChangeScene(string n) { sceneName = n; }
    }

    /// <summary>
    /// 请求 HUD 重新播报一次当前值。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要它：</b>HUD 面板是异步加载的（<c>UIMgr</c> 用协程加载预制体，至少晚一帧），
    /// 而"事实"事件只在值变化时发布 —— 于是面板加载完成时看到的是一屏空值。
    /// 面板在 <c>ShowMe</c> 里<b>先订阅、再发这条意图</b>，持有数据的系统收到后把当前值重播一遍。
    /// <para>这是"拉模型借道事件总线"：意图是祈使式、事实是过去式，订阅方不需要认识面板。</para>
    /// </remarks>
    public readonly struct RequestHudRefresh { }


    // Fact
    public readonly struct GamePaused { }

    public readonly struct GameResumed { }

    /// <summary>某一格的状态变了（含落回 <see cref="TileStateType.Normal"/>）。</summary>
    /// <remarks>
    /// 表现层据此换 Tilemap 上的贴图。逻辑层不认识 Tilemap，所以这条是"格子状态"唯一的对外出口。
    /// </remarks>
    public readonly struct TileStateChanged
    {
        /// <summary>格子坐标。</summary>
        public readonly Vector3Int Cell;

        /// <summary>切换后的状态。</summary>
        public readonly TileStateType State;

        public TileStateChanged(Vector3Int cell, TileStateType state)
        {
            Cell = cell;
            State = state;
        }
    }

    /// <summary>水球飞到玩家身上了。领取方（组合根）据此给资源 +1。</summary>
    public readonly struct WaterBallCollected { }

    /// <summary>水球数量变了。</summary>
    public readonly struct WaterBallCountChanged
    {
        /// <summary>当前数量。</summary>
        public readonly int Count;

        public WaterBallCountChanged(int count)
        {
            Count = count;
        }
    }

    /// <summary>玩家血量变了（含重置满血）。</summary>
    public readonly struct PlayerHealthChanged
    {
        /// <summary>当前血量。</summary>
        public readonly float Current;

        /// <summary>血量上限。</summary>
        public readonly float Max;

        public PlayerHealthChanged(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    /// <summary>波次或存活数变了。</summary>
    public readonly struct WaveChanged
    {
        /// <summary>当前波次序号（从 1 起）。</summary>
        public readonly int WaveIndex;

        /// <summary>场上存活敌人数。</summary>
        public readonly int Alive;

        public WaveChanged(int waveIndex, int alive)
        {
            WaveIndex = waveIndex;
            Alive = alive;
        }
    }
}
