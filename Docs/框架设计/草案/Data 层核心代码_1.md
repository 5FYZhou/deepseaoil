# Data 层核心代码 · 执行计划

先给分批计划，每批完成后停下来让你 review，确认后再进下一批。

| 批次       | 内容             | 原子点                                               |
| ---------- | ---------------- | ---------------------------------------------------- |
| **批次 1** | 基础设施         | `AsyncHandle` / `CacheEntry` / `CacheStore`          |
| **批次 2** | 计数与调度       | `RefCounter` / `AssetRegistry` / `LoadScheduler`     |
| **批次 3** | 生命周期与容错   | `LifecycleMgr` / `FailureHandler`                    |
| **批次 4** | AssetModule 整合 | `AssetModule` 对外入口                               |
| **批次 5** | ConfigModule     | `TablesHolder` / `StartupValidator` / `ConfigModule` |
| **批次 6** | DataMetrics      | `DataSnapshot` / `DataMetrics`                       |

**顺序依据**：从依赖树底部往上——`AsyncHandle` 无依赖，被所有加载路径使用；`AssetModule` 在批次 4 才整合，因为依赖批次 1~3 全部微块。

**本批范围**：批次 1，三个文件。开始。

---

## 批次 1：基础设施

### 1.1 AsyncHandle\<T\>

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 异步资源句柄。
    /// 调用时机：由 AssetModule.LoadAsync<T> 创建并返回。
    /// 边界：
    ///   - 必须是 class，不能是 struct（跨帧持有，框架约定）
    ///   - await 必须在主线程（Unity 资源 API 限制）
    ///   - Complete 只能调用一次，重复调用抛异常
    /// </summary>
    public sealed class AsyncHandle<T> where T : UnityEngine.Object
    {
        private readonly TaskCompletionSource<T> _tcs =
            new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDone { get; private set; }
        public T Asset { get; private set; }

        /// <summary>await 支持。调用方：var asset = await handle;</summary>
        public TaskAwaiter<T> GetAwaiter() => _tcs.Task.GetAwaiter();

        /// <summary>
        /// 加载完成时调用。
        /// 边界：调用方为 LoadScheduler 内部，不对外暴露。
        /// </summary>
        internal void Complete(T asset)
        {
            if (IsDone) throw new InvalidOperationException(
                $"[AsyncHandle] Complete called twice for {typeof(T).Name}");
            Asset = asset;
            IsDone = true;
            _tcs.SetResult(asset);
        }

        /// <summary>创建未完成的句柄。调用方：AssetModule.LoadAsync 未命中缓存时。</summary>
        internal static AsyncHandle<T> Create() => new AsyncHandle<T>();

        /// <summary>
        /// 创建已完成的句柄。
        /// 调用方：AssetModule.LoadAsync 命中缓存时。
        /// 边界：不经过 TaskCompletionSource 的异步路径，直接同步构造。
        /// </summary>
        internal static AsyncHandle<T> Completed(T asset)
        {
            var h = new AsyncHandle<T>();
            h.Asset = asset;
            h.IsDone = true;
            h._tcs.SetResult(asset);
            return h;
        }
    }
}
```

**三个设计点说明**：

1. **`RunContinuationsAsynchronously`**：避免 `await` 的后续代码在 `Complete` 的调用栈里同步执行，防止重入导致的状态混乱。
2. **`internal Complete`**：只允许 AssetModule 内部调用，调用方无法伪造句柄完成。
3. **`Completed` 静态工厂**：命中缓存时走这条路径，跳过异步链路，不产生额外 GC。

### 1.2 CacheEntry

```csharp
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 缓存条目。AssetModule 内部数据结构，不对外暴露。
    /// 生命周期：CacheStore 创建 → RefCounter 改 refCount → LifecycleMgr 改 canEvict → Evict 移除
    /// </summary>
    internal sealed class CacheEntry
    {
        /// <summary>资源本体。由 LoadScheduler 加载完成后写入。</summary>
        public UnityEngine.Object asset;

        /// <summary>引用计数。由 RefCounter 维护，不为负。</summary>
        public int refCount;

        /// <summary>最后访问时间（Time.realtimeSinceStartup）。每次 LoadAsync 命中 / TryGet 时更新。</summary>
        public float lastAccessTime;

        /// <summary>预加载标记。true 时 LifecycleMgr 永不淘汰。</summary>
        public bool isPreloaded;

        /// <summary>冷却期结束时间。refCount 归零时设为 now + COOLDOWN_SECONDS。</summary>
        public float cooldownUntil;

        /// <summary>是否可淘汰。冷却期结束后由 LifecycleMgr 置 true，LRU 只淘汰 canEvict=true 的条目。</summary>
        public bool canEvict;
    }
}
```

**为什么用 public 字段而非属性**：`CacheEntry` 是内部数据结构，只在 AssetModule 内部流转，属性封装在这里没有收益，反而增加噪音。字段直接访问是 C# 内部数据结构的常见做法。

### 1.3 CacheStore

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Jam.Data
{
    /// <summary>
    /// 缓存表。持有所有已加载资源条目。
    /// 调用时机：AssetModule 内部读写，不对外暴露。
    /// 边界：
    ///   - 主线程独占，无锁
    ///   - Key 为字符串，大小写敏感（与 Luban 的 path 校验保持一致）
    /// </summary>
    internal sealed class CacheStore
    {
        private readonly Dictionary<string, CacheEntry> _entries = new();

        public int Count => _entries.Count;

        /// <summary>
        /// 尝试取条目（不改变引用计数）。
        /// 调用方：RefCounter、LifecycleMgr、AssetModule.TryGet。
        /// </summary>
        public bool TryGetEntry(string key, out CacheEntry entry)
        {
            return _entries.TryGetValue(key, out entry);
        }

        /// <summary>
        /// 尝试取类型匹配的条目。
        /// 调用方：AssetModule.LoadAsync / TryGet。
        /// 边界：Key 存在但类型不匹配时返回 false（不抛异常）。
        /// </summary>
        public bool TryGet<T>(string key, out CacheEntry entry) where T : UnityEngine.Object
        {
            if (_entries.TryGetValue(key, out entry) && entry.asset is T)
                return true;
            entry = null;
            return false;
        }

        /// <summary>
        /// 写入条目。已存在则覆盖（调用方需保证不覆盖 refCount > 0 的条目）。
        /// 调用方：LoadScheduler 加载完成后。
        /// </summary>
        public void Put(string key, UnityEngine.Object asset, bool isPreloaded)
        {
            _entries[key] = new CacheEntry
            {
                asset = asset,
                refCount = 0,           // Retain 由调用方做
                lastAccessTime = Time.realtimeSinceStartup,
                isPreloaded = isPreloaded,
                cooldownUntil = 0f,
                canEvict = false,
            };
        }

        /// <summary>
        /// 更新访问时间。调用方：LoadAsync 命中 / TryGet 时。
        /// </summary>
        public void Touch(string key)
        {
            if (_entries.TryGetValue(key, out var e))
                e.lastAccessTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 移除条目。调用方：LifecycleMgr 淘汰时。
        /// 边界：调用方需保证 refCount == 0 && canEvict == true。
        /// </summary>
        public void Remove(string key)
        {
            _entries.Remove(key);
        }

        /// <summary>
        /// 遍历所有条目。调用方：LifecycleMgr.Tick。
        /// 边界：返回的是内部集合的引用，调用方不得在遍历时修改。
        /// </summary>
        public IEnumerable<KeyValuePair<string, CacheEntry>> AllEntries => _entries;

        /// <summary>
        /// 全部清空。调用方：AssetModule.Dispose。
        /// </summary>
        public void Clear() => _entries.Clear();
    }
}
```

**三个设计点说明**：

1. **`TryGet<T>` 的类型检查在 CacheStore 层**：Key 存在但类型不匹配时，返回 false 而不是抛异常——这允许同一 Key 被不同类型查询（虽然不应该发生），由调用方决定如何处理。
2. **`Touch` 独立成方法**：命中缓存时只需要更新访问时间，不需要取出条目，独立方法让调用路径更清晰。
3. **`AllEntries` 直接暴露内部集合**：`LifecycleMgr.Tick` 每帧遍历，避免产生临时数组。**风险**：调用方不能在遍历时修改集合——这个约束写在注释里，靠代码审查保证。

---

**批次 1 完成。** 三个文件：`AsyncHandle.cs` / `CacheEntry.cs` / `CacheStore.cs`。

请 review。确认后我进批次 2（`RefCounter` / `AssetRegistry` / `LoadScheduler`）。