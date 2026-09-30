# GameRoot / EventBus / Singleton · 伪代码示意

只示意 Data 层的接入预期，不做详细设计。

---

## 一、Singleton

```csharp
// 通用单例基类。Data 层的两个 Module 是 static class，不用此基类。
// 此基类服务于 Logic / Presentation 层的单例（Services / UIManager 等）。
public abstract class Singleton<T> where T : class, new()
{
    private static T _instance;
    public static T Instance => _instance ??= new T();
    public static void Reset() => _instance = null;
}
```

**Data 层接入预期**：ConfigModule / AssetModule 是 `static class`，**不继承 Singleton**。它们的"单例性"由 `static` 关键字保证，由 GameRoot 显式 Init。这种设计的原因是 Data 层需要明确的 Init 时序，而懒加载单例不能保证这一点。

---

## 二、EventBus

```csharp
// 跨层事件通道。T : struct 约束保留（值语义，无装箱，订阅方互不污染）。
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public static void Subscribe<T>(Action<T> handler) where T : struct { /* ... */ }
    public static void Unsubscribe<T>(Action<T> handler) where T : struct { /* ... */ }
    public static void Publish<T>(T evt) where T : struct { /* ... */ }
    public static void ClearAll() { /* 切场景时清空 */ }
}
```

**Data 层接入预期**：

- **ConfigModule 不 Publish / 不 Subscribe**——数据查询是同步的，无事件语义
- **AssetModule 不 Publish / 不 Subscribe**——加载完成通过 `AsyncHandle.await` 传递，不走事件
- **EventBus 与本层无关**：Data 层被所有层直连，不走跨层通道
- 未来若引入 `AssetLoaded` 观测事件，也是从 AssetModule 单向 Publish，**不改变 Data 层的零订阅契约**

---

## 三、GameRoot

```csharp
public class GameRoot : MonoBehaviour
{
    // 顺序表：初始化序 = 每帧序（框架核心约束）
    private readonly List<ITickable> _tickables = new();

    private void Awake()
    {
        // ─────────────────────────────────────────
        // 初始化：严格按序，Config 先于 Asset
        // ─────────────────────────────────────────
        ConfigModule.InitFromStreamingAssets();   // ① 同步，失败抛异常
        AssetModule.Init();                       // ② 建缓存表

        // 其余模块（示意）
        // PoolManager.Init();
        // InputProvider.Init();
        // UIManager.Init();
        // SceneService.Init();
        // DebugOverlay.Init();

        // ─────────────────────────────────────────
        // 写入 tickables[]，顺序 = 每帧执行序
        // ─────────────────────────────────────────
        _tickables.Add(new InputTickable());       // ① 采样
        _tickables.Add(new ServicesTickable());    // ② Pause/Save/Rng/Scene
        _tickables.Add(new AssetTickable());       // ③ AssetModule.Tick  ← Data 层
        _tickables.Add(new ActorsTickable());      // ④
        _tickables.Add(new ViewsTickable());       // ⑤
        _tickables.Add(new DebugTickable());       // ⑥
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _tickables.Count; i++)
            _tickables[i].Tick(dt);

        EventBus.Publish(new FrameEnded());
    }

    private void OnDestroy()
    {
        AssetModule.Dispose();
    }
}

// AssetModule 的 Tick 适配器（Data 层唯一被允许的主动行为）
internal sealed class AssetTickable : ITickable
{
    public void Tick(float dt) => AssetModule.Tick(dt);
}
```

**Data 层接入预期**：

| GameRoot 位置                  | 接入点                                                       | 顺序约束                                                  |
| ------------------------------ | ------------------------------------------------------------ | --------------------------------------------------------- |
| Awake 初始化                   | `ConfigModule.InitFromStreamingAssets()` → `AssetModule.Init()` | Config 先于 Asset（Asset 的预加载可能引用 Config 的 Key） |
| tickables[2]                   | `AssetModule.Tick(dt)`                                       | 在 Services 之后、Actors 之前                             |
| OnDestroy                      | `AssetModule.Dispose()`                                      | 进程退出，清缓存                                          |
| 切场景（由 SceneService 触发） | `AssetModule.OnSceneSwitch()`                                | SceneService 内部调用                                     |

---

## 四、三者与 Data 层的关系总览

```
┌────────────────────────────────────────────────────────┐
│  GameRoot                                              │
│  · 唯一 Update 入口                                    │
│  · Awake 装配顺序：Config → Asset → 其他               │
│  · tickables[2] = AssetModule.Tick                     │
│  · OnDestroy → AssetModule.Dispose                     │
└────────────────────────────────────────────────────────┘
              │ 驱动
              ▼
┌────────────────────────────────────────────────────────┐
│  Data 层                                               │
│  · ConfigModule（static，显式 Init，同步查询）         │
│  · AssetModule（static，显式 Init，异步 + Tick）       │
│  · DataMetrics（static，拉模型）                       │
│  · 不继承 Singleton                                    │
│  · 不订阅 EventBus                                     │
│  · 不被 EventBus 感知                                  │
└────────────────────────────────────────────────────────┘
              ▲ 直连
              │
┌────────────────────────────────────────────────────────┐
│  Logic / Presentation                                  │
│  · 通过 Singleton 拿到 Services / UIManager 等         │
│  · 通过 EventBus 跨层通信                              │
│  · 通过直连读 Data 层                                  │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  EventBus                                              │
│  · 只服务 Logic ↔ Presentation                         │
│  · Data 层不参与                                       │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  Singleton<T>                                          │
│  · 服务 Logic / Presentation 层的单例                  │
│  · Data 层不用（static class 已经保证唯一性）          │
└────────────────────────────────────────────────────────┘
```

---

## 五、三条边界（写进契约表）

| #    | 边界                                     | 理由                                          |
| ---- | ---------------------------------------- | --------------------------------------------- |
| 1    | Data 层不继承 Singleton，用 static class | 需要显式 Init 时序，懒加载单例无法保证        |
| 2    | Data 层不订阅 EventBus                   | 查询式接口无事件语义；加载完成通过 await 传递 |
| 3    | GameRoot 驱动 AssetModule.Tick           | Data 层唯一的主动行为，且仅处理内部状态       |

伪代码示意完毕。确认后可以进入下一阶段——要么补 GameRoot 的实际实现，要么先实操验证 Data 层代码。你定。