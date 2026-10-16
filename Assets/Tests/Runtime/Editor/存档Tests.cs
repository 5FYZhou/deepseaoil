// ---------------------------------------------------------------------------
// 存档往返：DTO 序列化 + 各地貌/玩家/波次子系统的导出回填
//
// 这里只覆盖不碰场景、不碰文件系统的部分：SaveService 的落盘与 SaveCaptureFeature
//   的抓帧都需要 Unity 运行时，留给 Play 模式手工验收。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Data.Settings;
using DeepseaOil.Logic.Grid;
using DeepseaOil.Logic.Grid.States;
using DeepseaOil.Logic.Player;
using DeepseaOil.Logic.Wave;
using NUnit.Framework;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Tests
{
    public class 存档Tests
    {
        [Test]
        public void 存档根能零损往返JSON()
        {
            var source = new GameSaveData
            {
                schemaVersion = GameSaveData.CurrentSchemaVersion,
                header = new SaveHeaderDTO
                {
                    schemaVersion = GameSaveData.CurrentSchemaVersion,
                    slotIndex = 2,
                    isAuto = false,
                    currentLevelId = 3,
                    waveIndex = 4,
                    savedAtUtc = "2026-10-16T12:00:00.0000000Z",
                    title = "存档槽 3",
                    timeText = "2026/10/16 20:00"
                },
                player = new PlayerSaveData
                {
                    posX = 1.5f,
                    posY = -2.25f,
                    faceX = 0f,
                    faceY = 1f,
                    hp = 2f,
                    water = 3,
                    earth = 4,
                    seedType = 1,
                    seedCount = 2
                },
                wave = new WaveSaveData
                {
                    waveIndex = 4,
                    cursor = 2,
                    pendingCursor = 1,
                    phase = 1,
                    timer = 7.5f,
                    pendingSeed = 3,
                    spawnTimer = 0.25f,
                    isCompleted = false,
                    settleDelayStarted = true
                },
                enemies = new List<EnemySaveData>
                {
                    new EnemySaveData { enemyId = 2, posX = 3f, posY = 4f, hp = 5 }
                },
                tiles = new List<TileSaveData>
                {
                    new TileSaveData { cellX = 1, cellY = 2, stateId = 2, remaining = 6.5f }
                },
                drops = new List<DropSaveData>
                {
                    new DropSaveData { type = 0, seed = 0, posX = -1f, posY = 0.5f }
                }
            };

            GameSaveData restored = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(source));

            Assert.IsNotNull(restored);
            Assert.IsNotNull(restored.header);
            Assert.AreEqual(source.schemaVersion, restored.schemaVersion);
            Assert.AreEqual(source.header.slotIndex, restored.header.slotIndex);
            Assert.AreEqual(source.header.savedAtUtc, restored.header.savedAtUtc);
            Assert.AreEqual(source.header.timeText, restored.header.timeText);
            Assert.AreEqual(source.player.posY, restored.player.posY, 1e-5f);
            Assert.AreEqual(source.wave.pendingSeed, restored.wave.pendingSeed);
            Assert.AreEqual(source.wave.settleDelayStarted, restored.wave.settleDelayStarted);

            Assert.AreEqual(1, restored.enemies.Count);
            Assert.AreEqual(5, restored.enemies[0].hp);

            Assert.AreEqual(1, restored.tiles.Count);
            Assert.AreEqual(6.5f, restored.tiles[0].remaining, 1e-4f);

            Assert.AreEqual(1, restored.drops.Count);
            Assert.AreEqual(0.5f, restored.drops[0].posY, 1e-4f);
        }

        [Test]
        public void 无倒计时地貌的剩余秒数为负一()
        {
            var state = new TableTileState(new TileStateSpec(RowFactory.TileStateRow(1)));

            Assert.AreEqual(-1f, state.RemainingDuration, 1e-6f);
        }

        [Test]
        public void 有倒计时地貌的剩余秒数随Tick递减()
        {
            var state = new TableTileState(new TileStateSpec(RowFactory.TileStateRow(2, duration: 10f)));

            Assert.AreEqual(10f, state.RemainingDuration, 1e-6f);

            state.OnTick(new TileContext(Vector3Int.zero, 0f, 4f, null, null));

            Assert.AreEqual(6f, state.RemainingDuration, 1e-6f);
        }

        [Test]
        public void 地貌导出回填保持状态与剩余秒数()
        {
            GridLogic grid = NewGrid();

            Assert.IsTrue(grid.SwitchTileState(Vector3Int.zero, TileStateType.Mud, 6f));
            Assert.IsTrue(grid.SwitchTileState(Vector3Int.right, TileStateType.Mud, 6f));

            List<TileSaveData> saved = grid.ExportTiles();

            Assert.AreEqual(2, saved.Count, "两格泥浆都该进存档");
            Assert.AreEqual(6f, saved[0].remaining, 1e-3f, "覆盖过的存续秒数该被导出");

            // 快照之外的格：回填必须把它清回空地，否则读档会叠着上一局的残留地貌
            Assert.IsTrue(grid.SwitchTileState(new Vector3Int(0, 1, 0), TileStateType.Mud, 6f));

            grid.RestoreTiles(saved);

            Assert.AreEqual(2, grid.ExportTiles().Count, "快照外的泥浆格没被清掉");
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Vector3Int.zero));
            Assert.AreEqual(TileStateType.Mud, grid.StateOf(Vector3Int.right));
            Assert.AreEqual(TileStateType.Normal, grid.StateOf(new Vector3Int(0, 1, 0)));
        }

        [Test]
        public void 玩家回填越界一律夹取()
        {
            PlayerStats stats = NewPlayerStats();

            stats.ImportSave(new PlayerSaveData
            {
                hp = 99f,
                water = 99,
                earth = -5,
                seedType = 99,
                seedCount = 99
            });

            Assert.AreEqual(3f, stats.Current, 1e-4f, "血量该夹到 max_hp");
            Assert.AreEqual(5, stats.Water, "水该夹到容量");
            Assert.AreEqual(0, stats.Earth, "负数该夹到 0");
            Assert.AreEqual(SeedType.None, stats.Seed, "非法种子枚举该落回 None");
            Assert.AreEqual(0, stats.SeedCount, "没有种子就不该留下次数");

            stats.ImportSave(new PlayerSaveData
            {
                hp = 1.5f,
                water = 2,
                earth = 3,
                seedType = (int)SeedType.Fire,
                seedCount = 2
            });

            Assert.AreEqual(1.5f, stats.Current, 1e-4f);
            Assert.AreEqual(2, stats.Water);
            Assert.AreEqual(3, stats.Earth);
            Assert.AreEqual(SeedType.Fire, stats.Seed);
            Assert.AreEqual(2, stats.SeedCount);
        }

        [Test]
        public void 波次导出回填保持相位与出怪游标()
        {
            WaveLogic logic = NewWaveLogic();

            logic.ImportSave(new WaveSaveData
            {
                waveIndex = 3,
                cursor = 0,
                pendingCursor = 1,
                phase = (int)WavePhase.Battle,
                timer = 12f,
                pendingSeed = (int)SeedType.Ice,
                spawnTimer = 0.4f,
                isCompleted = false,
                settleDelayStarted = false
            });

            WaveSaveData exported = logic.ExportSave();

            Assert.AreEqual(3, exported.waveIndex);
            Assert.AreEqual((int)WavePhase.Battle, exported.phase);
            Assert.AreEqual(1, exported.pendingCursor);
            Assert.AreEqual(12f, exported.timer, 1e-3f);
            Assert.AreEqual(1, logic.Remaining, "待刷数该由展开序列减游标推出来");
            Assert.IsTrue(logic.IsSpawning, "游标没到序列尾就该还在出怪");

            // 回填后第一次 Tick 不能重入 EnterPhase：那会把 pendingSeed 洗回 wave.grant_seed（本波为 1）
            logic.Tick(0.1f, true, new List<WaveLogic.SpawnRequest>());

            Assert.AreEqual((int)SeedType.Ice, logic.ExportSave().pendingSeed, "Tick 重入阶段把回填的种子洗掉了");
            Assert.AreEqual(1, logic.Remaining);
        }

        private static GridLogic NewGrid()
        {
            var specs = new List<TileStateSpec>
            {
                new TileStateSpec(RowFactory.TileStateRow(1)),
                new TileStateSpec(RowFactory.TileStateRow(2, duration: 10f))
            };

            var grid = new GridLogic(new GridGeometry(Vector2.zero, 1f), specs, CreateState);

            grid.RegisterCell(Vector3Int.zero);
            grid.RegisterCell(Vector3Int.right);
            grid.RegisterCell(new Vector3Int(0, 1, 0));

            return grid;
        }

        private static ITileState CreateState(TileStateType id)
        {
            switch (id)
            {
                case TileStateType.Mud:
                    return new TableTileState(new TileStateSpec(RowFactory.TileStateRow(2, duration: 10f)));

                case TileStateType.Normal:
                    return new TableTileState(new TileStateSpec(RowFactory.TileStateRow(1)));

                default:
                    return null;
            }
        }

        private static PlayerStats NewPlayerStats()
        {
            var config = ScriptableObject.CreateInstance<PlayerConfig>();

            var spec = new PlayerSpec(
                RowFactory.PlayerRow(), config, new ProjectileSpec(RowFactory.WaterRow(), null));

            return new PlayerStats(spec);
        }

        private static WaveLogic NewWaveLogic()
        {
            var enemies = new List<WaveEnemySpec>
            {
                new WaveEnemySpec(RowFactory.WaveEnemyRow(1, 1, 1, 2))
            };

            var waves = new List<WaveSpec>
            {
                new WaveSpec(RowFactory.WaveRow(1, grantSeed: 1), enemies)
            };

            return new WaveLogic(waves);
        }
    }
}
