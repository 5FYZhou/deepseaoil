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

    // 请求 HUD 重新播报一次当前值：面板异步加载（至少晚一帧），而事实事件只在值变化时发布，
    // 加载完成时看到的是一屏空值；面板在 ShowMe 里先订阅、再发这条意图，持有数据的系统收到后重播。
    // 意图是祈使式、事实是过去式，订阅方不需要认识面板。
    public readonly struct RequestHudRefresh { }


    // Fact
    public readonly struct GamePaused { }

    public readonly struct GameResumed { }

    // 某一格的状态变了。表现层据此换 Tilemap 贴图；逻辑层不认识 Tilemap，
    // 所以这是"格子状态"唯一的对外出口。
    public readonly struct TileStateChanged
    {
        public readonly Vector3Int Cell;

        public readonly TileStateType State;

        public TileStateChanged(Vector3Int cell, TileStateType state)
        {
            Cell = cell;
            State = state;
        }
    }

    // 瞄准变了的事实（发布方去重，只在真的变了时发）。瞄准结果是逻辑层的产出，不属于"请求某项能力"，
    // 因此走事实事件而不是端口。Available 是玩家侧口径：射程内 ＋ 冷却就绪 ＋ 有水球（土球是副攻击、不吃弹药）；
    // 世界侧接不接受由裁决回执决定，不进本事件，否则高亮会替世界侧提前回答。
    public readonly struct AimChanged
    {
        // 无鼠标 / 瞄不到格 / 暂停时为 false；带 false 时 Cell 无意义。
        public readonly bool HasAim;

        public readonly Vector3Int Cell;

        public readonly bool Available;

        public AimChanged(bool hasAim, Vector3Int cell, bool available)
        {
            HasAim = hasAim;
            Cell = cell;
            Available = available;
        }
    }

    // 掉落物被领取了的事实。数量在载荷里、不在订阅方：领取给什么由世界侧按 DropType 裁决，
    // 掉落物自己只发事实（世界 → 玩家只有"通知"一条路）。
    public readonly struct DropCollected
    {
        public readonly DropType Type;

        // 来自 DropDefinition.Amount。
        public readonly int Amount;

        public DropCollected(DropType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    public readonly struct WaterBallCountChanged
    {
        public readonly int Count;

        public WaterBallCountChanged(int count)
        {
            Count = count;
        }
    }

    /// <summary>玩家血量变了（含重置满血）。</summary>
    public readonly struct PlayerHealthChanged
    {
        public readonly float Current;

        public readonly float Max;

        public PlayerHealthChanged(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    // 波次或存活数变了；WaveIndex 从 1 起。
    public readonly struct WaveChanged
    {
        public readonly int WaveIndex;

        public readonly int Alive;

        public WaveChanged(int waveIndex, int alive)
        {
            WaveIndex = waveIndex;
            Alive = alive;
        }
    }
}
