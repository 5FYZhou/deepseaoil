using System;
using System.Collections.Generic;

using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Wave;
using UnityEngine;

namespace DeepseaOil.Logic.Level
{
    public enum LevelState
    {
        NotStarted = 0,
        Running = 1,
        Victory = 2,
        Defeat = 3,
    }

    public sealed class LevelLogic
    {
        private readonly WaveLogic _waveLogic;
        private readonly bool _deferVictoryEndEvent;
        private bool _victoryEndEventReleased;
        private bool _victoryEndEventPending;

        public LevelSpec CurrentLevel { get; }

        private int _nextLevelId;

        public WaveLogic Waves => _waveLogic;

        public WavePlayMode PlayMode { get; }

        public LevelState State { get; private set; } = LevelState.NotStarted;

        public bool IsFinished => State == LevelState.Victory || State == LevelState.Defeat;

        public LevelLogic(ILevelReader levelReader, bool deferVictoryEndEvent = false)
        {
            if (levelReader == null)
                throw new ArgumentNullException(nameof(levelReader));

            int currentLevelId = levelReader.CurrentLevelId;
            int nextLevelId = levelReader.NextLevelId;
            WavePlayMode playMode = levelReader.PlayMode;

            if (currentLevelId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(levelReader),
                    currentLevelId,
                    "Current level ID must be positive.");

            if (!Enum.IsDefined(typeof(WavePlayMode), playMode))
                throw new ArgumentOutOfRangeException(nameof(levelReader), playMode, "Unknown wave play mode.");

            CurrentLevel = ConfigModule.GetLevel(levelReader.CurrentLevelId);
            _nextLevelId = nextLevelId;
            PlayMode = playMode;
            _deferVictoryEndEvent = deferVictoryEndEvent;
            _waveLogic = new WaveLogic(CurrentLevel.Waves, PlayMode);
        }

        public void Start()
        {
            if (State != LevelState.NotStarted) return;

            _waveLogic.Reset();
            ChangeState(LevelState.Running);
        }

        public void Tick(
            float deltaTime,
            bool anyEnemyAlive,
            List<WaveLogic.SpawnRequest> spawnRequests)
        {
            if (spawnRequests == null)
                throw new ArgumentNullException(nameof(spawnRequests));

            spawnRequests.Clear();

            if (State != LevelState.Running) return;

            _waveLogic.Tick(deltaTime, anyEnemyAlive, spawnRequests);

            if (PlayMode == WavePlayMode.Sequential && _waveLogic.IsSequenceCompleted)
                ChangeState(LevelState.Victory);
        }

        public void Reset()
        {
            _waveLogic.Reset();
            _victoryEndEventPending = false;
            _victoryEndEventReleased = false;
            ChangeState(LevelState.NotStarted);
        }

        public bool ReleaseDeferredVictoryEndEvent()
        {
            if (!_deferVictoryEndEvent) return false;

            _victoryEndEventReleased = true;
            PublishDeferredVictoryEndEvent();
            return true;
        }

        public void ChangeState(LevelState state)
        {
            State = state;
            EventBus<LevelStateChanged>.Publish(new LevelStateChanged(State));
            if (state == LevelState.Defeat)
            {
                EventBus<LevelEnded>.Publish(new LevelEnded(CurrentLevel.Id, _nextLevelId));
                return;
            }

            if (state != LevelState.Victory) return;

            if (_deferVictoryEndEvent && !_victoryEndEventReleased)
            {
                _victoryEndEventPending = true;
                return;
            }

            PublishLevelEnded();
        }

        private void PublishDeferredVictoryEndEvent()
        {
            if (!_victoryEndEventPending || !_victoryEndEventReleased) return;

            _victoryEndEventPending = false;
            PublishLevelEnded();
        }

        private void PublishLevelEnded()
        {
            EventBus<LevelEnded>.Publish(new LevelEnded(CurrentLevel.Id, _nextLevelId));
        }

        public void Announce()
        {
            _waveLogic.Announce();
            EventBus<LevelStateChanged>.Publish(new LevelStateChanged(State));
        }
    }
}
