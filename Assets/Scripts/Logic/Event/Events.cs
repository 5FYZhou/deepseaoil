using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using cfg.dso;

namespace DeepseaOil.Logic.Events
{
    #region 请求
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

    // 请求 HUD 重新播报一次当前值：面板异步加载晚一帧，而事实事件只在变化时发布
    public readonly struct RequestHudRefresh { }

    #endregion

    #region 事实
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

    // 瞄准变了的事实，发布方去重；世界侧接不接受由裁决回执决定，不进本事件
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

    public readonly struct PlayerAmmoChanged
    {
        public readonly int Water;

        public readonly int WaterCapacity;

        public readonly int Earth;

        public readonly int EarthCapacity;

        public readonly SeedType Seed;

        public PlayerAmmoChanged(int water, int waterCapacity, int earth, int earthCapacity, SeedType seed)
        {
            Water = water;
            WaterCapacity = waterCapacity;
            Earth = earth;
            EarthCapacity = earthCapacity;
            Seed = seed;
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

    /// <summary>波次进入新阶段；备战期带配给种子与倒计时</summary>
    public readonly struct WavePhaseChanged
    {
        public readonly int WaveIndex;

        public readonly DeepseaOil.Logic.Wave.WavePhase Phase;

        public readonly SeedType GrantSeed;

        /// <summary>本阶段剩余秒数</summary>
        public readonly float SecondsLeft;

        public WavePhaseChanged(
            int waveIndex,
            DeepseaOil.Logic.Wave.WavePhase phase,
            SeedType grantSeed,
            float secondsLeft)
        {
            WaveIndex = waveIndex;
            Phase = phase;
            GrantSeed = grantSeed;
            SecondsLeft = secondsLeft;
        }
    }

    public readonly struct GridBlastOccurred
    {
        public readonly Vector2 Center;

        public readonly float Radius;

        public readonly TileStateType ResultTile;

        public GridBlastOccurred(Vector2 center, float radius, TileStateType resultTile)
        {
            Center = center;
            Radius = radius;
            ResultTile = resultTile;
        }
    }

    /// <summary>逐格瞬时特效：IsFire 为真＝火花，为假＝电弧</summary>
    public readonly struct GridPointVfxOccurred
    {
        public readonly Vector2 Point;

        public readonly bool IsFire;

        public GridPointVfxOccurred(Vector2 point, bool isFire)
        {
            Point = point;
            IsFire = isFire;
        }
    }

    public readonly struct CGCompleted
    {
        public readonly CGType type;
        public CGCompleted(CGType t) { type = t; }
    }

    #endregion
}
