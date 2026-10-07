using System;
using System.Collections.Generic;
using UnityEngine;
using cfg.demo;

namespace DeepseaOil.Data
{
    /// <summary>数值配置模块。Data 层的<b>唯一取值入口</b>：调用方只认识这里的 <c>GetXxx</c> 与它返回的包装件（数据来自 Excel 表还是 SO 文件，消费者不该关心）。</summary>
    public static class ConfigModule
    {
        private const string PlayerConfigKey = "config/PlayerConfig";

        /// <summary>关键表的主键默认值。当前每张表都只有一行（"默认"那一行）。</summary>
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

        public static bool IsReady => _ready;

        public static bool AreAssetsBound => _bound;

        /// <summary>初始化（第一段：只读表）。调用方：<c>GameRoot.Awake</c>，**必须早于 <c>AssetModule.Init</c>**；重复调用抛异常。</summary>
        /// <remarks>任何失败都包成 <see cref="ConfigLoadException"/> 抛出；<c>jsonRoot</c> 是 Luban 导出的 JSON 目录。</remarks>
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
                // Luban 生成代码在 JSON 结构不符时抛 SerializationException、文件缺失 / 为空在 TablesHolder 内抛 IOException 系：统一包成 ConfigLoadException，让 GameRoot 能区分「配置问题」与「代码问题」。
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

        public static void InitFromStreamingAssets()
        {
            Init(System.IO.Path.Combine(Application.streamingAssetsPath, "Luban"));
        }

        /// <summary>绑定 SO 资产（第二段）。调用方：<c>GameRoot.Assemble</c>，**必须在 <c>AssetModule.Init</c> 之后**；重复调用是 no-op（切场景 / 重复装配都不该重建一份）。</summary>
        /// <remarks>这一趟顺手把"资产缺了"炸在启动期：只读的 <c>PlayerConfig</c> 缺失是硬错误（走代码默认值的玩家会有 0 速度，查起来极慢）；三份观感调参缺资产只报警告 ＋ 走一份字段默认值。</remarks>
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

            // 三份观感调参自带兜底（丢资产时返回一份字段默认值 ＋ 一条 Warning）：它们不参与判定，只影响观感与手感，与 PlayerConfig 的"硬错误"是两种口径。
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

        // 玩法数值查询（包装件：表行 ＋ SO）。边界：id 不存在时 Luban 的 Get 会抛异常 —— 这里不 catch；
        // 表里少一行属于配置事故，应该在启动时就炸出来，而不是让敌人以速度 0 待机。

        /// <summary>读一个球种（表行 ＋ 投掷调参）。表中不存在该球种时返回 <c>null</c>。</summary>
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

        public static TileStateSpec GetTileState(TileStateType id)
        {
            EnsureAssets();

            return new TileStateSpec(_holder.Tables.TbTileState.Get(id));
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

        public static EnemySpec GetEnemy(int id = Ids.Enemy)
        {
            EnsureAssets();

            return new EnemySpec(_holder.Tables.TbEnemy.Get(id));
        }

        /// <summary>观感颜色表（SO）。<b>观感取值的单一权威入口</b>。</summary>
        /// <remarks><b>永不返回 <c>null</c></b>：<see cref="VisualPalette.LoadOrDefault"/> 丢资产时给一份字段默认值的实例 ＋ 一条 Warning（观感参数不参与判定，不能因为缺资产把游戏卡死），消费者不需要再写 <c>!= null</c> 兜底。球种色 / 敌人四态色 / 瞄准高亮两态色 / 贴地阴影色全部从这一处出去（要哪条语义就问包装件要，别把调色板本身传下去）。</remarks>
        public static VisualPalette Visuals
        {
            get
            {
                EnsureAssets();

                return _visuals;
            }
        }

        /// <summary>读玩家数值。它同时也是"玩家"这个取值的唯一入口：移动参数（SO）与水球的射程都从这里出去。</summary>
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

            return new WaveSpec(_holder.Tables.TbWave.Get(id));
        }

        /// <summary>全部关卡初始格子状态（返回生成行而不是包装件：每格一行的批量数据，消费者只做一次遍历）。</summary>
        /// <remarks><b>表里现在是 0 行</b>（<c>demo_tbtileinitial.json</c> 是 <c>[]</c>），也没有"关卡"维度：它是<b>待填充的基础设施</b>而不是零消费者残留 —— 关卡数据一进来消费端已经就位，<b>不要因为"表是空的"就删掉这一条链</b>。</remarks>
        public static IReadOnlyList<TileInitial> GetTileInitials()
        {
            EnsureAssets();

            return _holder.Tables.TbTileInitial.DataList;
        }

        public static DropSpec GetDrop()
        {
            EnsureAssets();

            return new DropSpec(_dropTuning);
        }

        // 示范表查询（白模时代的教学链）

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
        // 边界：只读；调用方不得跨帧持有该引用；**新增消费必须登记在本注释里**
        // ─────────────────────────────────────────────

        /// <summary>
        /// 原始生成表（<c>cfg.Tables</c>）。<b>只给诊断用</b>：正常取值一律走上面的 <c>GetXxx</c>。
        /// </summary>
        /// <remarks>
        /// <b>登记在案的破例只有一个</b>：<c>Presentation/Diagnostics/ConfigLoader.cs</c> 用它数三张示范表
        /// （<c>TbWeapon</c> / <c>TbItem</c> / <c>TbFish</c>）的行数，作为"导表链路通不通"的自检输出。
        /// <para>它给出的是<b>表对象</b>而不是行，调用方拿到之后只能数数；一旦有人拿它读某一列，
        /// 就绕过了包装件、也绕过了"表列迁到 SO"的全部收益。那种用法属于新增破例，必须先登记在这里。</para>
        /// </remarks>
        public static cfg.Tables Tables
        {
            get
            {
                EnsureReady();

                return _holder.Tables;
            }
        }

        // ─────────────────────────────────────────────
        // 「生成行不出 Data 层」的判据（可判定，替代过去的口头约定）
        //
        // ① 放行：生成**枚举**。`BallType` / `TileStateType` 是配置词汇本身
        //    （球种、格状态），包一层只会造出两个同义的平行类型。Data 层之外
        //    允许 `using cfg.demo;`，前提是该文件只吃这两个枚举类型。
        // ② 禁止：生成**行**（`cfg.demo.Projectile` / `Enemy` / `Player` / `Wave` /
        //    `TileState` / `TileInitial` …）。这类引用只准出现在 `Data/Config/**`。
        // ③ 例外：必须在此登记并说明理由。**当前例外为零** ——
        //    全库 `using cfg.demo;` 的 17 个非 Data 文件（含两处测试夹具）里只有一个
        //    真实用法形态：吃枚举做 switch / 字典键。一行都不碰生成行。
        //    `MudTileState` 看起来像例外，其实拿的是 `TileStateSpec`（包装件），不是 `TileState`。
        //
        // 判据怎么用：翻一个文件，问"它 `using cfg.demo;` 之后碰了什么类型"。
        // 只碰枚举 ⇒ ①；碰了行 ⇒ 必须先在这里登记；都不是 ⇒ 违纪。
        // ─────────────────────────────────────────────

        private static void EnsureReady()
        {
            if (!_ready)
                throw new InvalidOperationException("[Config] accessed before Init");
        }

        /// <remarks><b>刻意分开报错</b>：只报"没 Init"会把"忘了调 <c>BindAssets</c>"掩盖成同一个现象，而这两种装配错误的修法完全不同。</remarks>
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

    /// <summary>配置加载异常。独立定义，让 GameRoot 能区分「配置问题（重新导表）」与「代码问题」。</summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
