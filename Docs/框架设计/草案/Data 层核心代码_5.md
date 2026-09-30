# Data 层核心代码 · 批次 5

## 批次 5：ConfigModule

三个文件：`TablesHolder` / `StartupValidator` / `ConfigModule`。

**前置说明**：Luban 生成的 `cfg.Tables` 构造函数签名随代码目标（cs-simple-json / cs-newtonsoft-json）不同。以下代码以 `cs-newtonsoft-json` 为目标，Loader 返回 `Newtonsoft.Json.Linq.JObject`。如果你生成的是 simple-json 目标，`JObject` 改为 `SimpleJSON.JSONNode`，`JObject.Parse` 改为 `JSON.Parse`。**这是本批次唯一需要你按实际生成目标调整的地方**。

---

### 5.1 TablesHolder

```csharp
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 持有 Luban 生成的 Tables 实例。
    /// 调用时机：ConfigModule.Init 时构造一次。
    /// 边界：
    ///   - 构造时同步加载全部 JSON（启动时一次性完成）
    ///   - 构造失败抛异常，由 ConfigModule.Init 捕获并阻止游戏启动
    ///   - 版本差异：Android 平台需用 UnityWebRequest 从 StreamingAssets 读
    /// </summary>
    internal sealed class TablesHolder
    {
        public cfg.Tables Tables { get; }

        public TablesHolder(string jsonRoot)
        {
            if (string.IsNullOrEmpty(jsonRoot))
                throw new ArgumentException("[Config] jsonRoot is null or empty");

            if (!Directory.Exists(jsonRoot))
                throw new DirectoryNotFoundException($"[Config] jsonRoot not found: {jsonRoot}");

            // Luban 的 Tables 构造函数接收一个 Loader（Func<string, JObject>）
            // 版本差异：以实际生成的 Tables 构造函数签名为准
            Tables = new cfg.Tables(file => LoadJson(jsonRoot, file));
        }

        /// <summary>
        /// Luban 调用的 Loader。
        /// 输入：文件名（不含扩展名，如 "weapon"）
        /// 输出：解析后的 JSON 对象
        /// </summary>
        private static JObject LoadJson(string root, string file)
        {
            string path = Path.Combine(root, file + ".json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"[Config] JSON not found: {path}");

            string text = File.ReadAllText(path);

            // 边界：空文件视为错误，不静默返回空 JObject
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException($"[Config] JSON is empty: {path}");

            return JObject.Parse(text);
        }
    }
}
```

**三个设计点说明**：

1. **构造时全量加载**：`cfg.Tables` 的构造函数会调用 Loader 加载所有表。这是同步阻塞的，发生在 `GameRoot.Awake` 期间。Jam 阶段表少，耗时可控；量产阶段如果表多，可改为 Luban 的 lazy load 模式（`Tables` 延迟加载，首次访问时才读）。
2. **Loader 严格校验**：文件不存在、文件为空都抛异常。这些异常由 `ConfigModule.Init` 捕获，最终阻止游戏启动。**带病数据不允许进入运行时**。
3. **`Path.Combine` 处理跨平台**：Windows 用 `\`，macOS 用 `/`，`Path.Combine` 自动适配。

**Android 版本差异**（记录，不实现）：

`StreamingAssets` 在 Android 上位于 APK 内部，`File.ReadAllText` 无法直接读。需要用 `UnityWebRequest` 异步读取。Jam 阶段如果只跑 Windows/Mac，当前实现足够。**待你确认目标平台后再决定是否补 Android 分支**。

---

### 5.2 StartupValidator

```csharp
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 启动时校验。
    /// 调用时机：ConfigModule.Init 中，TablesHolder 构造完成后。
    /// 边界：
    ///   - 只确认"加载成功"，不重复 Luban 的 ref / path 校验
    ///   - 抽样访问关键表，不遍历全部（避免启动耗时）
    ///   - 校验失败返回 false，由 ConfigModule.Init 决定如何处理
    /// </summary>
    internal static class StartupValidator
    {
        /// <summary>
        /// 校验 Tables 可用性。
        /// 调用方：ConfigModule.Init。
        /// 边界：不抛异常；失败时通过返回值告知调用方。
        /// </summary>
        public static bool Validate(TablesHolder holder)
        {
            if (holder?.Tables == null)
            {
                Debug.LogError("[Config] Validate failed: Tables is null");
                return false;
            }

            // 抽样访问：触发关键表的加载与索引构造
            // 边界：如果 Luban 生成了新的表，这里需要同步新增
            //       这是本文件唯一的"硬编码"点，代价是启动时的主动确认
            try
            {
                _ = holder.Tables.TbWeapon.DataList;
                _ = holder.Tables.TbItem.DataList;
                // 按项目实际表继续添加
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Config] Validate failed during sampling: {e.Message}");
                return false;
            }

            return true;
        }
    }
}
```

**三个设计点说明**：

1. **抽样而非全遍历**：全遍历所有表在表多时会拖慢启动。抽样几张大表即可确认 Luban 的 Loader 工作正常。
2. **硬编码表名是刻意设计**：`TbWeapon` / `TbItem` 是项目实际存在的表，硬编码让"哪些表是关键的"成为显式决策。新增关键表时，这里同步添加——这是可接受的维护成本，比"自动遍历所有表"更可控。
3. **不重复 Luban 校验**：Luban 的 ref / path / range 校验在导表阶段已执行。运行时不需要重做。`StartupValidator` 只确认"数据能被加载进内存"。

---

### 5.3 ConfigModule

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 数值配置模块。Data 层的数值查询入口。
    /// 调用时机：
    ///   - Init：GameRoot.Awake 一次
    ///   - GetXxx：调用方任意时机
    /// 边界：
    ///   - 所有查询同步返回
    ///   - 运行时不接受任何写操作
    ///   - 不感知资源（资源引用是字符串 Key，由 AssetModule 消费）
    /// </summary>
    public static class ConfigModule
    {
        private static TablesHolder _holder;
        private static bool _ready;

        public static bool IsReady => _ready;

        /// <summary>
        /// 初始化。
        /// 调用方：GameRoot.Awake，早于 AssetModule.Init。
        /// 边界：
        ///   - 重复调用抛异常
        ///   - 校验失败抛异常，阻止游戏启动
        /// </summary>
        public static void Init(string jsonRoot)
        {
            if (_ready)
                throw new InvalidOperationException("[Config] ConfigModule.Init called twice");

            // 构造 Tables（同步加载全部 JSON）
            _holder = new TablesHolder(jsonRoot);

            // 启动校验
            if (!StartupValidator.Validate(_holder))
                throw new ConfigLoadException("[Config] startup validation failed");

            _ready = true;
            Debug.Log($"[Config] initialized, tables loaded from: {jsonRoot}");
        }

        /// <summary>
        /// 默认初始化：从 StreamingAssets/Luban 读取。
        /// 调用方：GameRoot.Awake。
        /// </summary>
        public static void InitFromStreamingAssets()
        {
            string root = System.IO.Path.Combine(Application.streamingAssetsPath, "Luban");
            Init(root);
        }

        // ─────────────────────────────────────────────
        // 查询接口（薄转发给 Luban 的 TbXxx）
        // 边界：id 不存在时 Luban 会抛异常；调用方需保证 id 有效
        // ─────────────────────────────────────────────

        public static cfg.weapon.Weapon GetWeapon(int id)
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.Get(id);
        }

        public static cfg.item.Item GetItem(int id)
        {
            EnsureReady();
            return _holder.Tables.TbItem.Get(id);
        }

        public static IReadOnlyList<cfg.weapon.Weapon> GetAllWeapons()
        {
            EnsureReady();
            return _holder.Tables.TbWeapon.DataList;
        }

        // ─────────────────────────────────────────────
        // 逃生舱：特殊情况直接访问原始 Tables
        // 边界：只读；调用方不得持有 Tables 引用超过当前帧
        // ─────────────────────────────────────────────

        public static cfg.Tables Tables
        {
            get
            {
                EnsureReady();
                return _holder.Tables;
            }
        }

        // ─────────────────────────────────────────────
        // 内部
        // ─────────────────────────────────────────────

        private static void EnsureReady()
        {
            if (!_ready)
                throw new InvalidOperationException("[Config] accessed before Init");
        }
    }

    /// <summary>
    /// 配置加载异常。用于区分"配置问题"与"其他运行时异常"。
    /// </summary>
    public class ConfigLoadException : Exception
    {
        public ConfigLoadException(string message) : base(message) { }
        public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
    }
}
```

**四个设计点说明**：

1. **`InitFromStreamingAssets` 作为便利方法**：默认路径是 `StreamingAssets/Luban`。测试时可以用 `Init(customRoot)` 传入任意路径。这是 Jam 阶段的最小设计，不引入配置化路径。
2. **`EnsureReady` 守卫**：所有查询方法先检查 `_ready`。如果调用方在 `Init` 之前查询，抛异常而非返回 null——**让错误在最早时刻暴露**。
3. **薄转发而非重新封装**：`GetWeapon` 直接调 `TbWeapon.Get(id)`，不做任何数据转换。ConfigModule 的价值在于**统一入口 + 生命周期管理 + 启动校验**，不在于数据形态改造。
4. **`Tables` 逃生舱**：暴露原始实例给特殊情况（如遍历所有表、访问未封装的表）。**代价**：调用方可能绕过 ConfigModule 直接调 Luban API，与"统一入口"的初衷冲突。**权衡**：Jam 阶段灵活性更重要，逃生舱保留。**约束写在注释里**：调用方不得持有 Tables 引用超过当前帧。

**异常类型的意义**：

`ConfigLoadException` 独立定义，让 GameRoot 可以区分处理：
- `ConfigLoadException` → 配置问题，通常是数据错误，需要重新导表
- 其他异常 → 代码问题，需要修代码

`GameRoot.Awake` 里可以做类似处理：

```csharp
try {
    ConfigModule.InitFromStreamingAssets();
} catch (ConfigLoadException e) {
    Debug.LogError($"[GameRoot] Config load failed: {e.Message}");
    // 阻止游戏启动
    Application.Quit();
}
```

---

## 批次 5 完成

三个文件：`TablesHolder.cs` / `StartupValidator.cs` / `ConfigModule.cs`。

**已完成的批次总览**：

| 批次 | 文件                                                 | 状态 |
| ---- | ---------------------------------------------------- | ---- |
| 1    | `AsyncHandle` / `CacheEntry` / `CacheStore`          | ✅    |
| 2    | `RefCounter` / `AssetRegistry` / `LoadScheduler`     | ✅    |
| 3    | `LifecycleMgr` / `FailureHandler`                    | ✅    |
| 4    | `AssetModule`                                        | ✅    |
| 5    | `TablesHolder` / `StartupValidator` / `ConfigModule` | ✅    |

**批次 5 的遗留事项**：

| 事项                                                         | 归属           |
| ------------------------------------------------------------ | -------------- |
| Luban 的 Loader 签名（JObject vs JSONNode）按实际生成目标调整 | 你实操时确认   |
| `TbWeapon` / `TbItem` 的实际类型名，按 Luban 生成的为准      | 你实操时确认   |
| Android 平台 StreamingAssets 读取方式                        | 待目标平台确认 |

请 review 批次 5。确认后我进批次 6（`DataSnapshot` / `DataMetrics`）——最后一个批次。