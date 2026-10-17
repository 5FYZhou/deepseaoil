using DeepseaOil.Logic.Service;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeepseaOil.Data;
using cfg.dso;
using DeepseaOil.Logic.Level;

namespace DeepseaOil.Logic.Events
{
    #region 请求

    public readonly struct RequestLoadScene
    {
        public readonly string sceneName;
        public readonly bool isAsync;
        public RequestLoadScene(string n, bool i) { sceneName = n; isAsync = i; }
    }


    public readonly struct RequestActiveScene 
    {
        public readonly string sceneName;
        public RequestActiveScene(string n) { sceneName = n; }
    }


    // 请求 HUD 重新播报一次当前值：面板异步加载晚一帧，而事实事件只在变化时发布
    public readonly struct RequestHudRefresh { }

    public readonly struct RequestSave
    {
        public readonly int SlotIndex;
        public readonly GameSaveData Data;

        public RequestSave(int slotIndex, GameSaveData data)
        {
            SlotIndex = slotIndex;
            Data = data;
        }
    }

    public readonly struct RequestAutoSave
    {
        public readonly GameSaveData Data;

        public RequestAutoSave(GameSaveData data)
        {
            Data = data;
        }
    }

    public readonly struct RequestLoad
    {
        public readonly int SlotIndex;

        public RequestLoad(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public readonly struct RequestDeleteSave
    {
        public readonly int SlotIndex;

        public RequestDeleteSave(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
    #endregion

    #region 事实
    public readonly struct SceneLoadStarted
    {
        public readonly string sceneName;
        public readonly bool isAsync;
        public SceneLoadStarted(string n, bool i) { sceneName = n; isAsync = i; }
    }

    public readonly struct SceneLoaded
    {
        public readonly string sceneName;
        public SceneLoaded(string n) { sceneName = n; }
    }

    /// 同步加载时 Load 完直接发；异步加载时在激活场景后发
    public readonly struct SceneActivated
    {
        public readonly string sceneName;
        public SceneActivated(string n) { sceneName = n; }
    }

    public readonly struct LoadingPanelReady
    {
        public readonly string sceneName;
        public LoadingPanelReady(string n) { sceneName = n; }
    }

    public readonly struct SceneTransitionFinished
    {
        public readonly string sceneName;
        public SceneTransitionFinished(string n) { sceneName = n; }
    }

    public readonly struct SceneTransitionFailed
    {
        public readonly string sceneName;
        public readonly string error;
        public SceneTransitionFailed(string n, string e) { sceneName = n; error = e; }
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

        public readonly TileStateType Previous;

        public readonly TileStateType State;

        public TileStateChanged(Vector3Int cell, TileStateType previous, TileStateType state)
        {
            Cell = cell;
            Previous = previous;
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

        public readonly SeedType Seed;

        public DropCollected(DropType type, int amount, SeedType seed = SeedType.None)
        {
            Type = type;
            Amount = amount;
            Seed = seed;
        }
    }

    public readonly struct PlayerAmmoChanged
    {
        public readonly int Water;

        public readonly int WaterCapacity;

        public readonly int Earth;

        public readonly int EarthCapacity;

        public readonly SeedType Seed;

        public readonly int SeedCount;

        public readonly int SeedCapacity;

        public PlayerAmmoChanged(
            int water,
            int waterCapacity,
            int earth,
            int earthCapacity,
            SeedType seed,
            int seedCount,
            int seedCapacity)
        {
            Water = water;
            WaterCapacity = waterCapacity;
            Earth = earth;
            EarthCapacity = earthCapacity;
            Seed = seed;
            SeedCount = seedCount;
            SeedCapacity = seedCapacity;
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


    public readonly struct WavePhaseChanged
    {
        public readonly int WaveIndex;

        public readonly DeepseaOil.Logic.Wave.WavePhase Phase;

        public readonly SeedType GrantSeed;


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

    public readonly struct WaveCountdownChanged
    {
        public readonly int WaveIndex;
        public readonly DeepseaOil.Logic.Wave.WavePhase Phase;
        public readonly int SecondsRemaining;

        public WaveCountdownChanged(
            int waveIndex,
            DeepseaOil.Logic.Wave.WavePhase phase,
            int secondsRemaining)
        {
            WaveIndex = waveIndex;
            Phase = phase;
            SecondsRemaining = secondsRemaining;
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

    public readonly struct LevelStateChanged
    {
        public readonly LevelState Level;

        public LevelStateChanged(LevelState level)
        {
            Level = level;
        }
    }

    public readonly struct LevelEnded
    {
        public readonly int curId;
        public readonly int nextId;

        public LevelEnded(int currentLevelId, int nextLevelId)
        {
            curId = currentLevelId;
            nextId = nextLevelId;
        }
    }

    public readonly struct SaveSlotsRefreshed
    {
        public readonly SaveSlotInfo[] Slots;

        public SaveSlotsRefreshed(SaveSlotInfo[] slots)
        {
            Slots = slots;
        }
    }

    public readonly struct SaveLoaded
    {
        public readonly int SlotIndex;
        public readonly GameSaveData Data;

        public SaveLoaded(int slotIndex, GameSaveData data)
        {
            SlotIndex = slotIndex;
            Data = data;
        }
    }

    public readonly struct SaveResult
    {
        public readonly int SlotIndex;
        public readonly bool Success;
        public readonly string Error;

        public SaveResult(int slotIndex, bool success, string error)
        {
            SlotIndex = slotIndex;
            Success = success;
            Error = error;
        }
    }

    public struct StoryFlowStateChanged
    {
        public string state;
        public StoryFlowStateChanged(string state) { this.state = state; }
    }
    #endregion
}
