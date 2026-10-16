using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic.Wave;
using NUnit.Framework;

namespace DeepseaOil.Tests
{
    public class WaveLogicTests
    {
        [Test]
        public void SpawnedWaveWithNoLivingEnemiesEntersSettleBeforeBattleTimerExpires()
        {
            var enemies = new List<WaveEnemySpec>
            {
                new WaveEnemySpec(RowFactory.WaveEnemyRow(1, 1, 1, 1)),
            };
            var waves = new List<WaveSpec>
            {
                new WaveSpec(RowFactory.WaveRow(), enemies),
            };
            var logic = new WaveLogic(waves);
            var spawnRequests = new List<WaveLogic.SpawnRequest>();

            logic.Tick(10f, false, spawnRequests);

            Assert.AreEqual(WavePhase.Battle, logic.Phase);
            Assert.AreEqual(1, spawnRequests.Count);

            logic.Tick(0f, true, spawnRequests);

            Assert.AreEqual(WavePhase.Battle, logic.Phase);

            logic.Tick(0f, false, spawnRequests);

            Assert.AreEqual(WavePhase.Settle, logic.Phase);
            Assert.AreEqual(8f, logic.PhaseRemaining);
        }

        [Test]
        public void WaveWithUnspawnedEnemiesRemainsInBattleWhenNoneAreAlive()
        {
            var enemies = new List<WaveEnemySpec>
            {
                new WaveEnemySpec(RowFactory.WaveEnemyRow(1, 1, 1, 2)),
            };
            var waves = new List<WaveSpec>
            {
                new WaveSpec(RowFactory.WaveRow(), enemies),
            };
            var logic = new WaveLogic(waves);
            var spawnRequests = new List<WaveLogic.SpawnRequest>();

            logic.Tick(10f, false, spawnRequests);
            logic.Tick(0f, false, spawnRequests);

            Assert.AreEqual(WavePhase.Battle, logic.Phase);
            Assert.AreEqual(0, spawnRequests.Count);
        }
    }
}
