# Data 层设计缺漏文档

本文件基于 Jam 场景（15 天有效开发、3 人团队、资源量小、内存压力低）与当前 Data 层设计，对 `ResMgr.cs` / `MonoMgr.cs` 对比分析暴露的缺口做**最终决策**。所有决策已由我做出，理由附在每一项下。

---

## 一、决策汇总

| #    | 缺口                                | 决策                       | 理由                                                         |
| ---- | ----------------------------------- | -------------------------- | ------------------------------------------------------------ |
| D1   | 重复入队保护                        | **修补**                   | Jam 场景会踩到（UI 快速开关、多 Actor 同 Key 请求），且修补成本低 |
| D2   | `Resources.UnloadUnusedAssets` 触发 | **修补**（累计淘汰后触发） | 修补成本极低；不补会让"淘汰"名不副实，量产阶段必踩           |
| D3   | 取消未完成异步加载                  | **不修补**                 | Jam 阶段加载队列短，浪费可忽略；修补成本中等（需改 `LoadRequest` 结构） |

---

## 二、D1：重复入队保护

### 决策：修补，采用「AssetModule 内 pending 字典」方案

**为什么不是"CacheEntry 加 loading 标志"**：那会改动 `CacheEntry` 结构与 `CacheStore.Put` 签名，牵扯面更大。用 pending 字典把合并逻辑集中在 `LoadAsync` 一处，改动面最小。

**为什么不是"LoadScheduler 去重"**：`LoadScheduler` 只处理调度，不知道 handle 的存在。合并回调的逻辑必须挂在 AssetModule 层。

### 决策依据

| 维度       | 说明                                                         |
| ---------- | ------------------------------------------------------------ |
| 触发概率   | 中高——UI 快速开关即触发；同一图标被多个 Actor 同时请求是常见场景 |
| 不补的后果 | 重复 IO；`refCount` 与实际持有者数量不符；`CacheEntry` 被第二次 `Put` 覆盖，第一次的句柄仍 resolve 但缓存已换 |
| 修补成本   | 低——AssetModule 内新增一个 `Dictionary<string, List<Action<Object>>>` |
| 影响范围   | 只改 `LoadAsync` 一个方法；`CacheEntry` / `CacheStore` / `LoadScheduler` 均不变 |

### 修补要点（不写代码，只描述）

- AssetModule 新增私有字段：`_pendingLoads: Dictionary<string, List<Action<UnityEngine.Object>>>`
- `LoadAsync` 未命中缓存时，先查 `_pendingLoads`：
  - 命中 → 把本次回调挂到列表，返回新 handle，不重新入队
  - 未命中 → 创建列表、写入、入队
- `LoadRequest.onDone` 触发时：`Put` 缓存 → 遍历列表逐一 `Retain` + `Complete` → 从 `_pendingLoads` 移除
- `LoadRequest.onFail` 触发时：`RecordFailure` → 遍历列表逐一 `Complete(fallback)` → 从 `_pendingLoads` 移除
- `Dispose` 时清空 `_pendingLoads`

### 已知边界（记录，不处理）

- 同 Key 不同类型（`LoadAsync<Sprite>("icon")` 与 `LoadAsync<Texture2D>("icon")`）会被合并为同一请求。Jam 阶段假设同路径即同类型，不处理此边界。若未来出现需要，在 Key 里加入类型后缀。

---

## 三、D2：`Resources.UnloadUnusedAssets` 触发

### 决策：修补，采用「淘汰后触发」方案

**为什么不是"每次淘汰后触发"**：`EvictLRU` 一次最多淘汰 8 个，若逐个触发会产生多次调用。改为在 `EvictLRU` 末尾一次性触发。

**为什么不是"切场景触发"**：`OnSceneSwitch` 只是标记 `canEvict`，真正的淘汰发生在下一帧 `Tick`。在 `OnSceneSwitch` 里触发 `UnloadUnusedAssets` 会跑在淘汰之前，回收不到任何东西。

**为什么不是"不补"**：`EvictLRU` 只做 `_cache.Remove(key)`，解除我们的引用。`CacheEntry.asset` 若无其他引用，会成为"未引用资源"，等 Unity 主动 GC。而 Unity 的自动 GC 时机不可控，量产阶段会导致内存峰值超预期。

### 决策依据

| 维度       | 说明                                                         |
| ---------- | ------------------------------------------------------------ |
| 触发概率   | 低（Jam 资源少，缓存可能不超过 100 条）                      |
| 不补的后果 | 淘汰"名不副实"——缓存条目删了，但内存没释放；量产阶段内存峰值不可控 |
| 修补成本   | 极低——`LifecycleMgr.EvictLRU` 末尾加一行                     |
| 影响范围   | 只改 `LifecycleMgr.EvictLRU` 一个方法                        |

### 修补要点（不写代码，只描述）

- `LifecycleMgr.EvictLRU` 在 `for` 循环结束后，若 `toEvict > 0`，调用 `Resources.UnloadUnusedAssets()`
- 忽略返回的 `AsyncOperation`——Jam 阶段不需要等待完成；Unity 会在后台处理
- 不在 `OnSceneSwitch` 里调用（时序错误）

### 已知边界（记录，不处理）

- `Resources.UnloadUnusedAssets` 是异步的，调用后不保证立即释放。对于 Jam 阶段内存压力小的场景，这没问题。量产阶段若需要精确控制，改为 `yield return` 等待完成。

---

## 四、D3：取消未完成异步加载

### 决策：不修补

### 决策依据

| 维度       | 说明                                                         |
| ---------- | ------------------------------------------------------------ |
| 触发概率   | 低——Jam 阶段加载队列短（并发 4），面板开关间隔通常长于加载时长 |
| 不补的后果 | 浪费一次 IO 和反序列化；加载队列长时可能阻塞后续请求。Jam 阶段两者都不明显 |
| 修补成本   | 中——需改 `LoadRequest` 加取消标志；`LoadScheduler.Tick` 启动前检查；要处理"已启动但未完成"的取消路径 |
| 权衡       | 修补成本 > Jam 阶段的收益                                    |

### 何时重新评估

- DebugOverlay 显示"加载中"数量持续 > 10
- 或 Profiler 显示 `Resources.LoadAsync` 的等待时间成为瓶颈
- 或加载队列长度经常触及并发上限

---

## 五、通过旧代码对比确认的设计正确性（不需要修改）

以下设计点通过与 `ResMgr.cs` 对比得到确认，记录为设计依据。

| #    | 设计点               | 旧代码做法                                     | 当前设计                         | 结论                                         |
| ---- | -------------------- | ---------------------------------------------- | -------------------------------- | -------------------------------------------- |
| V1   | 引用归零后的卸载时机 | `isDel` 由外部传入，调用方决定是否立即删       | 冷却期自动管理                   | 当前设计更好——调用方不需要思考"什么时候该删" |
| V2   | 每帧驱动入口         | `MonoMgr` 提供 AddUpdateListener，任何类都能挂 | GameRoot 顺序表统一驱动          | 当前设计更好——时序可推理                     |
| V3   | 异步加载返回形态     | `UnityAction<T>` 回调                          | `AsyncHandle<T>` + await         | 当前设计更好——无回调地狱，异常路径清晰       |
| V4   | Key 的构成           | `path + "_" + typeof(T).Name`                  | 纯字符串 Key，类型由泛型参数指定 | 当前设计更好——同路径不同 T 可共享条目        |
| V5   | 同步 / 异步混合      | `ResMgr.Load` 发现加载中则停协程改同步         | 只提供异步                       | 当前设计更好——避免迁就同步 API 的补丁        |

---

## 六、不吸收的旧代码模式（ADR 记录）

以下模式与当前框架冲突，明确不吸收。`ResMgr.cs` / `MonoMgr.cs` 两份文件**不进新工程**。

| 模式                                      | 不吸收理由                                                   |
| ----------------------------------------- | ------------------------------------------------------------ |
| `MonoMgr` 全局 Update 分发                | 与"GameRoot 唯一 Update 入口"根本冲突，诱导绕过时序约束      |
| `SingletonAutoMono<T>` 继承               | Data 层用 static class 显式 Init                             |
| 协程做异步加载                            | 加载状态散在 `IEnumerator` 里不可观测；与 `AsyncHandle` 设计冲突 |
| `UnityAction` 回调 API                    | 回调地狱；错误处理不清晰                                     |
| `BaseManager<T>` 继承                     | Data 层不用继承式单例                                        |
| 同步加载 API                              | 已定只提供异步                                               |
| `UnloadAsset` 的 `isSub` / `isDel` 双开关 | 把"何时释放"的决策权交给调用方，违反"Data 层自动管理生命周期"的定位 |

---

## 七、修补后的 Data 层文件清单（增量标记）

```
Assets/Scripts/Data/
├── ConfigModule/
│   ├── ConfigModule.cs
│   ├── TablesHolder.cs
│   └── StartupValidator.cs
├── AssetModule/
│   ├── AssetModule.cs            ← D1 修补：新增 _pendingLoads 字段 + LoadAsync 改造
│   ├── AssetRegistry.cs
│   ├── CacheStore.cs
│   ├── CacheEntry.cs
│   ├── RefCounter.cs
│   ├── LoadScheduler.cs
│   ├── LifecycleMgr.cs           ← D2 修补：EvictLRU 末尾触发 UnloadUnusedAssets
│   ├── FailureHandler.cs
│   └── AsyncHandle.cs
└── DataMetrics/
    ├── DataMetrics.cs
    └── DataSnapshot.cs
```

**修补只触及两个文件**：`AssetModule.cs` 与 `LifecycleMgr.cs`。

---

## 八、待办清单（修补顺序）

| #    | 项                                 | 归属         | 优先级           |
| ---- | ---------------------------------- | ------------ | ---------------- |
| 1    | D1 修补 `AssetModule.LoadAsync`    | AssetModule  | 高——影响主流程   |
| 2    | D2 修补 `LifecycleMgr.EvictLRU`    | LifecycleMgr | 中——不影响主流程 |
| 3    | 实操验证 D1 修补后无重复入队       | —            | 高               |
| 4    | 观察 DebugOverlay 确认 D2 触发时机 | —            | 低               |

---

## 九、之前遗留项汇总（非本次分析得出）

以下项在批次 4 / 5 / 6 已记录，此处仅做索引。

| 项                                                           | 归属   | 状态             |
| ------------------------------------------------------------ | ------ | ---------------- |
| Luban Loader 签名（JObject vs JSONNode）                     | 批次 5 | 待实操确认       |
| `cfg.Tables` 表访问器实际类型名                              | 批次 5 | 待实操确认       |
| `cfg.Tables` 是否有稳定的表数量属性                          | 批次 6 | 待实操确认       |
| `Resources.LoadAsync(path, typeof(UnityEngine.Object))` 类型过滤 | 批次 4 | 待实操验证       |
| Android StreamingAssets 读取方式                             | 批次 5 | 待目标平台确认   |
| `AsyncHandle.Completed` 的 GC 开销                           | 批次 4 | 待 Profiler 验证 |
| `LoadScheduler` 重试是否改为延时                             | 批次 2 | 待评估           |

---

## 十、本文件的状态

- **D1 / D2 的修补方案已定稿**，可直接进入实现
- **D3 明确不修补**，记录为已知限制
- **五条设计确认（V1~V5）** 记录为设计依据，不修改
- **六条 ADR** 记录为不吸收模式，`ResMgr.cs` / `MonoMgr.cs` 不进新工程
