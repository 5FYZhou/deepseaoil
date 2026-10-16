using System;
using System.Collections.Generic;
using DeepseaOil.Data.Settings;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Data
{
        /// <summary>数值配置模块，Data 层唯一取值入口：调用方只用 GetXxx 与包装件</summary>
        // 生成行只准出现在 Data/Config/**，破例须登记
    public static class ConfigModule
    {
        private const string PlayerConfigKey = "config/PlayerConfig";
        private const string UIConfigKey = "config/UIConfig";
        private const string AudioConfigKey = "config/AudioConfig";

        /// <summary>关键表主键默认值</summary>
        public static class Ids
        {
            public const int Player = 1;

            public const int Enemy = 1;

            public const int Wave = 1;
        }

        private static TablesHolder _holder;
        private static bool _ready;
        private static bool _bound;

        private static PlayerConfig _playerConfig;
        private static ThrowTuning _throwTuning;
        private static DropTuning _dropTuning;
        private static VisualPalette _visuals;
        private static VisualFeelTuning _feel;
        private static EnemyTuning _enemyTuning;
        private static UIConfig _uiConfig;
        private static AudioConfig _audioConfig;

        /// <summary>元素反应规则，装配期折成 (原格, 球种) 哈希表；此后只读</summary>
        private static IReadOnlyList<ElementRuleSpec> _elementRules;

        /// <summary>二级元素反应查询表，装配期建一次；此后只读</summary>
        private static DuoReactionCatalog _duoReactions;

        /// <summary>种子基建配置，种子号→包装件；装配期装一次，此后只读</summary>
        private static Dictionary<SeedType, SeedSpec> _seeds;

        /// <summary>波次→敌种清单，装配期折一次（值按子表行序）；此后只读</summary>
        private static Dictionary<int, List<WaveEnemySpec>> _waveEnemies;

        /// <summary>没配敌种的波共用这一份，避免每次分配新空表</summary>
        private static readonly List<WaveEnemySpec> EmptyWaveEnemies = new List<WaveEnemySpec>();

        private static Dictionary<int, List<EnemyDropSpec>> _enemyDrops;

        private static readonly List<EnemyDropSpec> EmptyEnemyDrops = new List<EnemyDropSpec>();

        public static bool IsReady => _ready;

        public static bool AreAssetsBound => _bound;

        /// <summary>初始化只读表：必须早于 AssetModule.Init，重复调用抛异常</summary>
        public static void Init(string jsonRoot)
        {
            if (_ready)
                throw new InvalidOperationException("[Config] ConfigModule.Init called twice");

            if (string.IsNullOrEmpty(jsonRoot))
                throw new ConfigLoadException("[Config] jsonRoot is null or empty");

            if (!System.IO.Directory.Exists(jsonRoot))
                throw new ConfigLoadException($"[Config] jsonRoot not found: {jsonRoot}");

            try
            {
                _holder = new TablesHolder(jsonRoot);
            }
            catch (Exception e)
            {
                // 一切异常统一包成 ConfigLoadException：让 GameRoot 能区分配置问题与代码问题。
                throw new ConfigLoadException($"[Config] load failed: {e.Message}", e);
            }

            if (!StartupValidator.Validate(_holder))
            {
                _holder = null;
                throw new ConfigLoadException("[Config] startup validation failed");
            }

            _ready = true;
        }

        public static void InitFromStreamingAssets()
        {
            Init(System.IO.Path.Combine(Application.streamingAssetsPath, "Luban"));
        }

        /// <summary>绑定 SO 资产：必须在 AssetModule.Init 之后，重复调用是 no-op</summary>
        /// <remarks>PlayerConfig 缺失是硬错误（玩家速度为 0），观感调参缺失只报警告</remarks>
        public static void BindAssets()
        {
            EnsureReady();

            if (_bound) return;

            if (!AssetModule.IsInitialized)
            {
                Debug.LogError(
                    "[Config] BindAssets 在 AssetModule.Init 之前被调用：SO 读不到，已跳过。" +
                    "调用顺序必须是 ConfigModule.Init → AssetModule.Init → ConfigModule.BindAssets。");
                return;
            }

            _playerConfig = AssetModule.Load<PlayerConfig>(PlayerConfigKey);

            if (_playerConfig == null)
            {
                Debug.LogError(
                    $"[Config] 取不到 {PlayerConfigKey}（期望 Assets/Resources/{PlayerConfigKey}.asset）：" +
                    "玩家移动参数会全部是字段默认值（字段级默认值不是策划填的那一套）。");
            }

            _throwTuning = ThrowTuning.LoadOrDefault();
            _dropTuning = DropTuning.LoadOrDefault();
            _visuals = VisualPalette.LoadOrDefault();
            _feel = VisualFeelTuning.LoadOrDefault();

            // enemy 表已不存这些值：缺了没有字段默认值可退，会静默改变手感，故当场抛
            _enemyTuning = AssetModule.Load<EnemyTuning>(EnemyTuning.ResourceKey);

            if (_enemyTuning == null)
            {
                throw new ConfigLoadException(
                    $"[Config] 取不到 {EnemyTuning.ResourceKey}（期望 Assets/Resources/{EnemyTuning.ResourceKey}.asset）：" +
                    "敌人的判定半径 / 加速度 / 击退衰减 / 停止距离 / 脱战距离全在这份 SO 里，表里已经没有这些值可退。");
            }

            _uiConfig = AssetModule.Load<UIConfig>(UIConfigKey);
            if (_uiConfig == null)
            {
                Debug.LogError(
                    $"[Config] 取不到 {_uiConfig}（期望 Assets/Resources/{UIConfigKey}.asset）：" +
                    "ui配置参数会全部是字段默认值（字段级默认值不是策划填的那一套）。");
            }

            _audioConfig = AssetModule.Load<AudioConfig>(AudioConfigKey);
            if (_audioConfig == null)
            {
                Debug.LogError(
                    $"[Config] 取不到 {_audioConfig}（期望 Assets/Resources/{AudioConfigKey}.asset）：" +
                    "ui配置参数会全部是字段默认值（字段级默认值不是策划填的那一套）。");
            }

            _bound = true;

            // 启动期走一遍：表里少一行应在这里炸，而不是等第一次投掷
            ProjectileSpec water = GetBall(BallType.Water);

            if (water == null)
                throw new ConfigLoadException($"[Config] projectile 表里没有球种 {BallType.Water}，投掷链无法工作");

            _ = GetPlayer();
            _ = GetEnemy();
            _ = GetWave();
            _ = GetDrop();

            // 元素层与种子表也在启动期走一遍：少一行、少一条规则在这里炸，而不是等第一次投掷
            _ = GetElementRules();
            _ = GetDuoReactions();
            _ = GetAllSeeds();
            _ = GetLevels();

            // 精英怪是预留行：表里有就必须折得出来（多敌种刷怪接上前不能静默缺数据）
            _ = GetEnemy(2);

            // 每一波的敌种清单也在启动期走一遍：wave_enemy 少一行、enemy_id 写错在这里炸，而不是等那一波开打
            IReadOnlyList<Wave> waveRows = _holder.Tables.TbWave.DataList;

            for (int i = 0; i < waveRows.Count; i++)
            {
                IReadOnlyList<WaveEnemySpec> enemies = GetWaveEnemies(waveRows[i].Id);

                for (int j = 0; j < enemies.Count; j++)
                {
                    _ = GetEnemy(enemies[j].EnemyId);

                    _ = GetEnemyDrops(enemies[j].EnemyId);
                }
            }
        }

        // 玩法数值查询（包装件：表行 ＋ SO）；id 不存在时 Luban 的 Get 抛异常

        /// <summary>读一个球种，表里没有返回 null</summary>
        public static ProjectileSpec GetBall(BallType type)
        {
            EnsureAssets();

            if (!_holder.Tables.TbProjectile.DataMap.TryGetValue(type, out Projectile row)) return null;

            return new ProjectileSpec(row, _throwTuning);
        }

        public static IReadOnlyList<ProjectileSpec> GetAllBalls()
        {
            EnsureAssets();

            IReadOnlyList<Projectile> rows = _holder.Tables.TbProjectile.DataList;

            var result = new List<ProjectileSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new ProjectileSpec(rows[i], _throwTuning));
            }

            return result;
        }

        /// <summary>读一个格子状态，表里没有抛异常</summary>
        public static TileStateSpec GetTileState(TileStateType id)
        {
            EnsureAssets();

            return new TileStateSpec(_holder.Tables.TbTileState.Get(id));
        }

        /// <summary>读一个格子状态，表里没有返回 null 不抛异常</summary>
        public static TileStateSpec TryGetTileState(TileStateType id)
        {
            EnsureAssets();

            TileState row = _holder.Tables.TbTileState.GetOrDefault(id);

            return row != null ? new TileStateSpec(row) : null;
        }

        public static IReadOnlyList<TileStateSpec> GetAllTileStates()
        {
            EnsureAssets();

            IReadOnlyList<TileState> rows = _holder.Tables.TbTileState.DataList;

            var result = new List<TileStateSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new TileStateSpec(rows[i]));
            }

            return result;
        }

        /// <summary>全部元素反应规则；顺序无语义，查询由 ReactionResolver 负责</summary>
        public static IReadOnlyList<ElementRuleSpec> GetElementRules()
        {
            EnsureAssets();

            if (_elementRules != null) return _elementRules;

            IReadOnlyList<ElementRule> rows = _holder.Tables.TbElementRule.DataList;

            var rules = new List<ElementRuleSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                rules.Add(new ElementRuleSpec(rows[i]));
            }

            _elementRules = rules;

            return _elementRules;
        }

        /// <summary>二级元素跨界反应查询表；装配期建一次，调表要重启</summary>
        public static DuoReactionCatalog GetDuoReactions()
        {
            EnsureAssets();

            if (_duoReactions != null) return _duoReactions;

            IReadOnlyList<ElementDuoReaction> rows = _holder.Tables.TbElementDuoReaction.DataList;

            var rules = new List<DuoReactionSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                rules.Add(new DuoReactionSpec(rows[i]));
            }

            _duoReactions = new DuoReactionCatalog(rules);

            return _duoReactions;
        }

        /// <summary>读一个种子，表里没有返回 null 不抛异常</summary>
        public static SeedSpec GetSeed(SeedType id)
        {
            EnsureAssets();

            EnsureSeeds();

            return _seeds.TryGetValue(id, out SeedSpec spec) ? spec : null;
        }

        /// <summary>全部种子配置，装配期用一次即可</summary>
        public static IReadOnlyList<SeedSpec> GetAllSeeds()
        {
            EnsureAssets();

            EnsureSeeds();

            var result = new List<SeedSpec>(_seeds.Count);

            foreach (KeyValuePair<SeedType, SeedSpec> pair in _seeds)
            {
                result.Add(pair.Value);
            }

            return result;
        }

        /// <summary>读一个敌人种类：耐久来自 enemy 表，运动学来自 EnemyTuning 条目</summary>
        public static EnemySpec GetEnemy(int id = Ids.Enemy)
        {
            EnsureAssets();

            _enemyTuning.TryGet(id, out EnemyTuningEntry entry);

            return new EnemySpec(_holder.Tables.TbEnemy.Get(id), entry);
        }

        /// <summary>某波的敌种清单，按 wave_enemy 行序；这一波没配返回空表不抛异常</summary>
        public static IReadOnlyList<WaveEnemySpec> GetWaveEnemies(int waveId)
        {
            EnsureAssets();

            EnsureWaveEnemies();

            return _waveEnemies.TryGetValue(waveId, out List<WaveEnemySpec> list) ? list : EmptyWaveEnemies;
        }

        public static IReadOnlyList<EnemyDropSpec> GetEnemyDrops(int enemyId)
        {
            EnsureAssets();

            EnsureEnemyDrops();

            return _enemyDrops.TryGetValue(enemyId, out List<EnemyDropSpec> list) ? list : EmptyEnemyDrops;
        }

        /// <summary>观感颜色表（SO），观感取值唯一入口</summary>
        /// <remarks>永不返回 null：丢资产时给字段默认值实例＋Warning</remarks>
        public static VisualPalette Visuals
        {
            get
            {
                EnsureAssets();

                return _visuals;
            }
        }

        /// <summary>战斗观感调参（SO）：受击/震屏/扬尘取值的唯一入口</summary>
        public static VisualFeelTuning VisualFeel
        {
            get
            {
                EnsureAssets();

                return _feel;
            }
        }

        /// <summary>净空皮厚（世界单位）：净空 &gt; 体半径 + 皮厚才可通行</summary>
        public static float NavSkin
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.navSkin;
            }
        }

        /// <summary>净空烘焙解析上限：查询盒半边长与净空饱和值都是它，必须大于最大体半径</summary>
        public static float NavClearanceCap
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.navClearanceCap;
            }
        }

        /// <summary>入弯最低速度倍率：路线方向与当前速度对齐度为 0 时的目标速度倍率</summary>
        public static float CornerSpeedFactor
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.cornerSpeedFactor;
            }
        }

        /// <summary>软分离生效距离（世界单位）</summary>
        public static float SeparationRadius
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.separationRadius;
            }
        }

        /// <summary>软分离权重：与流场方向相加后归一，越大越容易挤开流场方向</summary>
        public static float SeparationWeight
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.separationWeight;
            }
        }

        /// <summary>卡死判定帧数：速度被压到零满这么多物理帧才算卡死</summary>
        public static int StuckFrames
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.stuckFrames;
            }
        }

        /// <summary>卡死换步持续帧数：这段时间走次优邻格，绕开卡住的那条边</summary>
        public static int JamFrames
        {
            get
            {
                EnsureAssets();

                return _enemyTuning.jamFrames;
            }
        }

        /// <summary>读玩家数值，"玩家"取值的唯一入口：移动参数（SO）与水球射程都从这里出去</summary>
        public static PlayerSpec GetPlayer(int id = Ids.Player)
        {
            EnsureAssets();

            return new PlayerSpec(
                _holder.Tables.TbPlayer.Get(id),
                _playerConfig,
                GetBall(BallType.Water));
        }

        public static WaveSpec GetWave(int id = Ids.Wave)
        {
            EnsureAssets();

            return new WaveSpec(_holder.Tables.TbWave.Get(id), GetWaveEnemies(id));
        }

        /// <summary>全部波次配置，按表内顺序；WaveLogic 逐波推进，跑完回第 1 行</summary>
        public static IReadOnlyList<WaveSpec> GetWaves()
        {
            EnsureAssets();

            IReadOnlyList<Wave> rows = _holder.Tables.TbWave.DataList;

            var result = new List<WaveSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new WaveSpec(rows[i], GetWaveEnemies(rows[i].Id)));
            }

            return result;
        }

        public static LevelSpec GetLevel(int id)
        {
            EnsureAssets();

            cfg.dso.LevelWave row = _holder.Tables.TbLevelWave.GetOrDefault(id);

            if (row == null)
                throw new ConfigLoadException($"[Config] level_wave 表里没有关卡 id={id}");

            if (row.WaveIds == null || row.WaveIds.Count == 0)
                throw new ConfigLoadException($"[Config] 关卡 id={id} 没有配置任何波次");

            var waves = new List<WaveSpec>(row.WaveIds.Count);

            for (int i = 0; i < row.WaveIds.Count; i++)
            {
                int waveId = row.WaveIds[i];
                Wave wave = _holder.Tables.TbWave.GetOrDefault(waveId);

                if (wave == null)
                    throw new ConfigLoadException(
                        $"[Config] 关卡 id={id} 引用了不存在的 wave id={waveId}（wave_ids 第 {i + 1} 项）");

                waves.Add(new WaveSpec(wave, GetWaveEnemies(waveId)));
            }

            return new LevelSpec(row, waves);
        }

        public static IReadOnlyList<LevelSpec> GetLevels()
        {
            EnsureAssets();

            IReadOnlyList<cfg.dso.LevelWave> rows = _holder.Tables.TbLevelWave.DataList;
            var result = new List<LevelSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(GetLevel(rows[i].Id));
            }

            return result;
        }

        /// <summary>全部关卡初始格子状态；表当前 0 行，不要因表空删链</summary>
        public static IReadOnlyList<TileInitial> GetTileInitials()
        {
            EnsureAssets();

            return _holder.Tables.TbTileInitial.DataList;
        }

        /// <summary>取一种掉落物的数值；种类没有独立数值行，全部共用 DropTuning</summary>
        public static DropSpec GetDrop(DropType type = DropType.Water)
        {
            EnsureAssets();

            return new DropSpec(_dropTuning, type);
        }

        public static UIConfig GetUIConfig()
        {
            EnsureAssets();
            return _uiConfig;
        }

        public static AudioConfig GetAudioConfig()
        {
            EnsureAssets();
            return _audioConfig;
        }

        public static IReadOnlyList<AudioMapSpec> GetAudioMaps()
        {
            EnsureAssets();

            IReadOnlyList<cfg.dso.AudioMap> rows = _holder.Tables.TbAudioMap.DataList;
            var result = new List<AudioMapSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new AudioMapSpec(rows[i]));
            }

            return result;
        }

        // 逃生舱：特殊情况直接访问原始 Tables
        // 边界：只读；调用方不得跨帧持有该引用；新增消费必须登记在本注释里

        /// <summary>原始生成表，只给诊断用，正常取值一律走 GetXxx</summary>
        /// <remarks>当前无登记破例。新增破例必须先登记在这里</remarks>
        public static cfg.Tables Tables
        {
            get
            {
                EnsureReady();

                return _holder.Tables;
            }
        }

        private static void EnsureReady()
        {
            if (!_ready)
                throw new InvalidOperationException("[Config] accessed before Init");
        }

        /// <summary>种子配置缓存，首次访问装一次</summary>
        private static void EnsureSeeds()
        {
            if (_seeds != null) return;

            IReadOnlyList<Seed> rows = _holder.Tables.TbSeed.DataList;

            _seeds = new Dictionary<SeedType, SeedSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                _seeds[rows[i].Id] = new SeedSpec(rows[i]);
            }
        }

        /// <summary>波次敌种表缓存，首次访问折一次</summary>
        private static void EnsureWaveEnemies()
        {
            if (_waveEnemies != null) return;

            IReadOnlyList<WaveEnemy> rows = _holder.Tables.TbWaveEnemy.DataList;

            _waveEnemies = new Dictionary<int, List<WaveEnemySpec>>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                WaveEnemy row = rows[i];

                if (!_waveEnemies.TryGetValue(row.WaveId, out List<WaveEnemySpec> list))
                {
                    list = new List<WaveEnemySpec>(4);

                    _waveEnemies[row.WaveId] = list;
                }

                list.Add(new WaveEnemySpec(row));
            }
        }

        private static void EnsureEnemyDrops()
        {
            if (_enemyDrops != null) return;

            IReadOnlyList<EnemyDrop> rows = _holder.Tables.TbEnemyDrop.DataList;

            _enemyDrops = new Dictionary<int, List<EnemyDropSpec>>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                EnemyDrop row = rows[i];

                if (!_enemyDrops.TryGetValue(row.EnemyId, out List<EnemyDropSpec> list))
                {
                    list = new List<EnemyDropSpec>(4);

                    _enemyDrops[row.EnemyId] = list;
                }

                list.Add(new EnemyDropSpec(row));
            }
        }

        /// <remarks>刻意分开报错：只报"没 Init"会把"忘了调 BindAssets"掩盖成同一现象</remarks>
        private static void EnsureAssets()
        {
            EnsureReady();

            if (!_bound)
            {
                throw new InvalidOperationException(
                    "[Config] 玩法数值在 BindAssets 之前被读取：调用顺序必须是 " +
                    "ConfigModule.Init → AssetModule.Init → ConfigModule.BindAssets");
            }
        }
    }

    /// <summary>配置加载异常，独立定义让 GameRoot 能区分配置问题（重新导表）与代码问题</summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
