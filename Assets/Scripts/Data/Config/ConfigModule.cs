using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>
    /// 数值配置模块。Data 层的<b>唯一取值入口</b>：调用方只认识这里的 <c>GetXxx</c> 与它返回的包装件，
    /// <b>数据来自 Excel 表还是 SO 文件，消费者不该关心</b>。
    /// </summary>
    /// <remarks>
    /// <b>两段装配，顺序不能反：</b>
    /// <list type="number">
    /// <item><see cref="Init"/> / <see cref="InitFromStreamingAssets"/> —— 只读表。
    /// 契约是"必须早于 <c>AssetModule.Init</c>"（<c>GameRoot.Awake</c>）。</item>
    /// <item><see cref="BindAssets"/> —— 绑 SO（<c>PlayerConfig</c> / <c>ThrowTuning</c> / <c>DropTuning</c>）。
    /// 契约是"必须在 <c>AssetModule.Init</c> 之后"。</item>
    /// </list>
    /// 拆两段的原因：SO 的读取要走 <c>AssetModule</c>，而表早于 <c>AssetModule</c> ——
    /// 一段式 Init 会让"表"和"SO"互相成为对方的装配前置条件。
    /// <para><b>边界：</b>所有查询同步返回；运行时不接受任何写操作；不感知资源（资源引用是字符串 Key，
    /// 交给 <c>AssetModule</c>）；<c>Init</c> 失败抛异常阻止游戏启动（带病数据不进运行时），
    /// 而 <c>BindAssets</c> 缺 SO 时只报警告 ＋ 走代码默认值（调参不参与判定，只影响观感与手感）。</para>
    /// </remarks>
    public static class ConfigModule
    {
        // ─────────────────────────────────────────────
        // 资产 Key（走 AssetModule；键与资产位置成对，见各 SO 的类注释）
        // ─────────────────────────────────────────────

        /// <summary>玩家移动参数的资产 Key：<c>Assets/Resources/config/PlayerConfig.asset</c>。</summary>
        private const string PlayerConfigKey = "config/PlayerConfig";

        // ─────────────────────────────────────────────
        // 表主键默认值（取代旧的 SpecCatalog.Default*Id）
        // ─────────────────────────────────────────────

        /// <summary>关键表的主键默认值。当前每张表都只有一行（"默认"那一行）。</summary>
        public static class Ids
        {
            /// <summary>默认玩家编号（<c>player</c> 表主键）。</summary>
            public const int Player = 1;

            /// <summary>默认敌人编号（<c>enemy</c> 表主键）。</summary>
            public const int Enemy = 1;

            /// <summary>默认波次编号（<c>wave</c> 表主键）。</summary>
            public const int Wave = 1;
        }

        private static TablesHolder _holder;
        private static bool _ready;
        private static bool _bound;

        private static PlayerConfig _playerConfig;
        private static ThrowTuning _throwTuning;
        private static DropTuning _dropTuning;
        private static VisualPalette _visuals;

        /// <summary>表是否已就绪（<c>Init</c> 成功）。</summary>
        public static bool IsReady => _ready;

        /// <summary>SO 是否已绑定（<c>BindAssets</c> 成功）。</summary>
        public static bool AreAssetsBound => _bound;

        // ─────────────────────────────────────────────
        // 装配
        // ─────────────────────────────────────────────

        /// <summary>
        /// 初始化（第一段：只读表）。调用方：<c>GameRoot.Awake</c>，**必须早于 <c>AssetModule.Init</c>**。
        /// </summary>
        /// <param name="jsonRoot">Luban 导出的 JSON 目录。</param>
        /// <remarks>边界：重复调用抛异常；任何失败都包成 <see cref="ConfigLoadException"/> 抛出。</remarks>
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
                // Luban 生成代码在 JSON 结构不符时抛 SerializationException；
                // 文件缺失 / 为空在 TablesHolder 内抛 IOException 系。
                // 统一包成 ConfigLoadException，让 GameRoot 能区分「配置问题」与「代码问题」。
                throw new ConfigLoadException($"[Config] load failed: {e.Message}", e);
            }

            if (!StartupValidator.Validate(_holder))
            {
                _holder = null;
                throw new ConfigLoadException("[Config] startup validation failed");
            }

            _ready = true;
            Debug.Log($"[Config] initialized, tables loaded from: {jsonRoot}");
        }

        /// <summary>默认初始化：从 StreamingAssets/Luban 读取。调用方：<c>GameRoot.Awake</c>。</summary>
        public static void InitFromStreamingAssets()
        {
            Init(System.IO.Path.Combine(Application.streamingAssetsPath, "Luban"));
        }

        /// <summary>
        /// 绑定 SO 资产（第二段）。调用方：<c>GameRoot.Assemble</c>，**必须在 <c>AssetModule.Init</c> 之后**。
        /// </summary>
        /// <remarks>
        /// <b>幂等</b>：重复调用是 no-op（切场景 / 重复装配都不该重建一份）。
        /// <para><b>这一趟顺手把"资产缺了"炸在启动期</b>：只读的 <c>PlayerConfig</c> 缺失是硬错误
        /// （走代码默认值的玩家会有 0 速度，查起来极慢）；两份调参是安全网（见各自 SO 的 <c>LoadOrDefault</c>）。</para>
        /// </remarks>
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

            // 三份观感调参自带兜底（丢资产时返回一份字段默认值 ＋ 一条 Warning）：它们不参与判定，
            // 只影响观感与手感，所以与 PlayerConfig 的"硬错误"是两种口径。
            _throwTuning = ThrowTuning.LoadOrDefault();
            _dropTuning = DropTuning.LoadOrDefault();
            _visuals = VisualPalette.LoadOrDefault();

            _bound = true;

            // 启动期把资产依赖的取值走一遍：表里少一行应该在这里炸，而不是等第一次投掷。
            ProjectileSpec water = GetBall(BallType.Water);

            if (water == null)
                throw new ConfigLoadException($"[Config] projectile 表里没有球种 {BallType.Water}，投掷链无法工作");

            _ = GetPlayer();
            _ = GetEnemy();
            _ = GetWave();
            _ = GetDrop();
        }

        // ─────────────────────────────────────────────
        // 玩法数值查询（包装件：表行 ＋ SO）
        // 边界：id 不存在时 Luban 的 Get 会抛异常 —— 这里不 catch。
        // 表里少一行属于配置事故，应该在启动时就炸出来，而不是让敌人以速度 0 待机。
        // ─────────────────────────────────────────────

        /// <summary>读一个球种（表行 ＋ 投掷调参）。表中不存在该球种时返回 <c>null</c>。</summary>
        public static ProjectileSpec GetBall(BallType type)
        {
            EnsureAssets();

            if (!_holder.Tables.TbProjectile.DataMap.TryGetValue(type, out Projectile row)) return null;

            return new ProjectileSpec(row, _throwTuning, _visuals);
        }

        /// <summary>全部球种（按表顺序）。</summary>
        /// <remarks>不缓存：表是进程级常驻的只读对象，调用点是装配期各一次，缓存反而要多维护一份状态。</remarks>
        public static IReadOnlyList<ProjectileSpec> GetAllBalls()
        {
            EnsureAssets();

            IReadOnlyList<Projectile> rows = _holder.Tables.TbProjectile.DataList;

            var result = new List<ProjectileSpec>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                result.Add(new ProjectileSpec(rows[i], _throwTuning, _visuals));
            }

            return result;
        }

        /// <summary>读一个格子状态。</summary>
        public static TileStateSpec GetTileState(TileStateType id)
        {
            EnsureAssets();

            return new TileStateSpec(_holder.Tables.TbTileState.Get(id));
        }

        /// <summary>全部格子状态（按表顺序）。</summary>
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

        /// <summary>读一个敌人种类。</summary>
        public static EnemySpec GetEnemy(int id = Ids.Enemy)
        {
            EnsureAssets();

            return new EnemySpec(_holder.Tables.TbEnemy.Get(id), _visuals);
        }

        /// <summary>读玩家数值。</summary>
        /// <remarks>它同时也是"玩家"这个取值的唯一入口：移动参数（SO）与水球的射程都从这里出去。</remarks>
        public static PlayerSpec GetPlayer(int id = Ids.Player)
        {
            EnsureAssets();

            return new PlayerSpec(
                _holder.Tables.TbPlayer.Get(id),
                _playerConfig,
                GetBall(BallType.Water));
        }

        /// <summary>读波次数值。</summary>
        public static WaveSpec GetWave(int id = Ids.Wave)
        {
            EnsureAssets();

            return new WaveSpec(_holder.Tables.TbWave.Get(id));
        }

        /// <summary>
        /// 全部关卡初始格子状态。
        /// </summary>
        /// <remarks>
        /// <b>返回生成行而不是包装件</b>：它是"每格一行"的批量数据，消费者
        /// （<c>GridLogic.LoadInitialStates</c>）只做一次遍历，包装一层没有语义收益。
        /// 当前表里 0 行数据、也没有"关卡"维度（见缺陷登记 N5）。
        /// </remarks>
        public static IReadOnlyList<TileInitial> GetTileInitials()
        {
            EnsureAssets();

            return _holder.Tables.TbTileInitial.DataList;
        }

        /// <summary>读一种掉落物。</summary>
        public static DropSpec GetDrop()
        {
            EnsureAssets();

            return new DropSpec(_dropTuning, _visuals);
        }

        // ─────────────────────────────────────────────
        // 示范表查询（白模时代的教学链，保留）
        // ─────────────────────────────────────────────

        public static Weapon GetWeapon(int id)
        {
            EnsureReady();

            return _holder.Tables.TbWeapon.Get(id);
        }

        public static Item GetItem(int id)
        {
            EnsureReady();

            return _holder.Tables.TbItem.Get(id);
        }

        public static Fish GetFish(int id)
        {
            EnsureReady();

            return _holder.Tables.TbFish.Get(id);
        }

        public static IReadOnlyList<Weapon> GetAllWeapons()
        {
            EnsureReady();

            return _holder.Tables.TbWeapon.DataList;
        }

        // ─────────────────────────────────────────────
        // 逃生舱：特殊情况直接访问原始 Tables
        // 边界：只读；调用方不得跨帧持有该引用
        // ─────────────────────────────────────────────

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

        /// <summary>
        /// 玩法取值的守卫：表与 SO 都必须就绪。
        /// </summary>
        /// <remarks>
        /// <b>刻意分开报错</b>：只报"没 Init"会把"忘了调 <c>BindAssets</c>"掩盖成同一个现象，
        /// 而这两种装配错误的修法完全不同。
        /// </remarks>
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

    /// <summary>
    /// 配置加载异常。独立定义，让 GameRoot 能区分「配置问题（重新导表）」与「代码问题」。
    /// </summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
