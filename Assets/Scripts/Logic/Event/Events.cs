using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using cfg.dso;

namespace DeepseaOil.Logic.Events
{
    // Intent
    public readonly struct RequestPause { }

    public readonly struct RequestResume { }

    /// <summary> 请求加载某场景</summary>
    public readonly struct RequestLoadScene
    {
        public readonly string sceneName;
        public readonly bool isAsync;
        public RequestLoadScene(string n, bool i) { sceneName = n; isAsync = i; }
    }

    /// <summary> 请求激活某场景（异步加载时用），若该场景未加载会报错 </summary>
    public readonly struct RequestActiveScene 
    {
        public readonly string sceneName;
        public RequestActiveScene(string n) { sceneName = n; }
    }


    // 请求 HUD 重新播报一次当前值。面板异步加载至少晚一帧，而事实事件只在值变化时发布，加载完成时看到空值；
    // 面板先订阅再发本条意图，持有数据的系统收到后重播。
    public readonly struct RequestHudRefresh { }

    /// <summary> 某场景异步加载完毕，等待被激活 </summary>
    public readonly struct SceneLoaded
    {
        public readonly string sceneName;
        public SceneLoaded(string n) { sceneName = n; }
    }

    /// <summary> 某场景被激活了。</summary>
    /// <remarks>同步加载时，Load完直接发；
    /// 异步加载时，会在监听到RequestActiveScene并激活场景后发</remarks>
    public readonly struct SceneActivated
    {
        public readonly string sceneName;
        public SceneActivated(string n) { sceneName = n; }
    }

    public readonly struct GamePaused { }

    public readonly struct GameResumed { }

    public readonly struct GameStateChanged
    {
        public readonly GameState state;
        public GameStateChanged(GameState state) { this.state = state; }
    }

    // 某一格状态变了，表现层据此换 Tilemap 贴图，也是"格子状态"唯一的对外出口。
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

    // 瞄准变了的事实，发布方去重。Available 是玩家侧口径：射程内 ＋ 冷却就绪 ＋ 有水球；
    // 世界侧接不接受由裁决回执决定，不进本事件。
    public readonly struct AimChanged
    {
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

    // 掉落物被领取了的事实，数量在载荷里不在订阅方。
    public readonly struct DropCollected
    {
        public readonly DropType Type;

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

    public readonly struct CGCompleted
    {
        public readonly CGType type;
        public CGCompleted(CGType t) { type = t; }
    }
}
