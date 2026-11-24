--- START OF TASK ---
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-2026-0003
- Plan Version: v01
- Status: APPROVED
- Task Level: L2 / Risk Level: R2
- Project / Engine / Platform: 荒原复苏指南 (deepseaoil) / Unity 2022.3.62f3c1 (URP 14.0.12, 2D Renderer) / Windows Standalone & Unity Editor
- Branch / Source Base Commit: `ai/GD-2026-0003` / `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`（施工 Worktree 从批准提交后的 `VReason/Update` 分出；批准提交仅增加任务文档，不改变源码基线；**Luban 导表与 `comment-budget.json` 合入须待批 1 合入主线后再接力合并**）
- Planner: Planner Chat（`planner-orchestrator` + `game-systems-designer` + `unity2d-architect` + `task-brief-writer`）
- Worker: 独立 Worker 会话（Agent 模式，禁止触碰批 1/批 2 独占文件）
- Watchdog: 独立 Watchdog 会话（`$watchdog-inspector`，不得与 Worker 共用会话）
- Context: `AI_Context/TASKS/GD-2026-0003/00_CONTEXT_MANIFEST.md`（待 Secretary 登记生成）+ Base Commit `df60015f9d333a2e122f9cfca97fc3b289e0c1f8`
- Execution Attempt Cap: A01–A03
- Created At: 2026-11-25

## 1. Objective
完成新手引导、对话弹窗体验与战斗 HUD 辅助信息升级（批 3 / 3），且所有程序生成/复用资源均通过可配置接口解耦以便后续美术与音效无缝替换，实现以下可观察且可验证的结果：
1. **通俗化引导文案**：重构 `slot_dialogue` 文案，减少/消除晦涩科幻专有名词，改用“砸”“水球/土块/生命泉/元素种子”等直观表述暗示抛物线与反应玩法，补正并明示 `Shift / Space` 冲刺与各交互键位。
2. **弹窗与文字体验升级**：
   - `DialoguePanel` 弹窗（`NpcPopup` / `PlayerPopup`）启用 `Typewriter` 逐字渐出；
   - 新增打字机“哒哒哒”节流短音与弹窗进/出滑行轻音效（优先走配置资源，无资产时由 `AudioManager` 缓存程序合成音兜底）；
   - 弹窗从屏幕边缘平滑滑入滑出，并在停留期间带持续轻微“跷跷板”摇动；
   - 弹窗停留退出时机由可见字符数/打字机完结动态计算（替换写死的 `3f` 秒），并保持暂停与读档兼容。
3. **HUD 槽位键位标**：`GamePlayPanel` 水/土/种子三槽明确显示 `LMB`、`RMB`、`E`（及持种时可选 `Q` 提示）键位标识。
4. **屏幕边缘水珠导航图标**：当水井、土丘、生命泉超出相机可视范围时，在屏幕边缘显示朝向目标的水珠型导航图标（优先走 `AssetModule` 资源 Key，无图时由程序生成平滑水滴 Sprite 兜底）。
5. **独立轻量 HUD 浮窗（不阻塞 `StoryFlow`）**：
   - 波前 `Prep` 发放战备种子时弹出轻量支援提示，说明种子来源与用法；
   - 玩家长时间处于残血且不在生命泉内时，智能弹出回血去向与方式提示（带冷却与状态守卫）。

## 2. Non-goals
1. 本批**不修改**批 1 独占文件（`SceneObstacleBaker.cs`、`NavField.cs`、`TileObstacleDirector.cs`、`TilemapAdapter.cs`、`LifeFountainState.cs`、`Fountain.cs`、`TileGridLightmap.cs`、`EnemyTuning.asset`、`VisualFeelTuning.asset`、`enemy.xlsx`、`tile_state.xlsx`、`element_rule.xlsx`、`element_duo_reaction.xlsx`）。
2. 本批**不修改**批 2 独占文件（`PlayerStats.cs`、`PlayerController.cs`、`CombatRoot.cs`）。
3. **不做**抛物线弧线预测显示、**不做**靶场模式入口、**不改变**生命神泉不加世界环形读条的决策。
4. **不将**种子配给提醒、残血提醒塞入 `StoryFlow` / `DialoguePanel` 阻塞剧情通道；**不破坏** `StoryFlow` 1002 段硬编码的 `line.Id = 10, 11, 12, 13` 镜头聚焦索引。
5. **不清理**非本批的既有红基线文件（`TimerWheel.cs`、`SaveSlot.cs`）。

## 3. Current State
- `[VERIFIED]` **弹窗无打字机且写死 3 秒**：`Assets/Scripts/Presentation/Story/StoryFlow.cs:46,410` 硬编码 `PopupDurationSeconds = 3f`，`DialoguePanel.ShowPopup`（`Assets/Scripts/Presentation/UI/Panel/DialoguePanel.cs:96-119`）未调用 `Typewriter.Play(...)`，仅调用 `UIFader.FadeIn(0.18f)`；`Typewriter.cs` 无逐字回调与完结查询属性。
- `[VERIFIED]` **1002 段存在硬编码行号**：`Assets/Scripts/Presentation/Story/StoryFlow.cs:364-376` 对 `paragraph == 1002` 硬编码监听 `line.Id == 10`（水泉）、`11`（土泉）、`12`（生命泉）、`13`（恢复跟随）。改 `slot_dialogue.xlsx` 时绝不能增删打乱这些行号。
- `[VERIFIED]` **现有引导文本重度专有名词**：`Assets/StreamingAssets/Luban/dso_tbslotdialogue.json` 使用大量“能量溶剂提取器 / 荒原基质 / 活性重构仪 / 原素结晶”，`id=23` 仅提示直接瞄准虫子而非砸地面打反应，`id=50` 误称精英“有几率掉落”（实表 `probability = 1` 必掉）。
- `[VERIFIED]` **同 Clip 去重会拦截“哒哒哒”**：`Assets/Scripts/Presentation/Audio/AudioManager.cs:376-379` 禁止同 `AudioClip` 在 `_playing` 中叠加；且 `AudioManager.cs` 当前处于 `comment-lint.ps1` 红基线（预算 `718`）。
- `[VERIFIED]` **HUD 与泉眼公开入口已就绪（无需改 `CombatRoot.cs`）**：
  - `CombatRoot.Current?.Fountains` 已公开只读列表，每个 `Fountain` 已暴露 `Kind`、`PlayerInside` 与 `transform.position`；
  - `EventBus<WavePhaseChanged>` 已携带 `GrantSeed`；`EventBus<PlayerAmmoChanged>` 已广播 `Seed` / `SeedCount`；`EventBus<PlayerHealthChanged>` 已广播 `Current` / `Max`。
- `[VERIFIED]` **注释预算紧绷文件**：`Tools/comment-budget.json` 中 `DialoguePanel.cs` 预算为 `0`、`AudioConfig.cs` 为 `0`、`GamePlayPanel.cs` 为 `21`、`AudioManager.cs` 为 `718`（既有超标）。
- `[VERIFIED]` **无散乱 `Update()` 纪律**：`AGENTS.md` §5 规定业务组件严禁私自写 `Update()`，所有渲染帧驱动应走 `GameRoot`（`IRenderTicked`）或既定服务 Tick（`UIMgr` / 协程）。
- `[UNKNOWN]` `Assets/Resources/ui/` 中 `GamePlayPanel` / `DialoguePanel` 预制体的实际层级命名、`UIMgr.cs` 面板驱动结构、`AudioConfig.cs` 字段现状以及 `Assets/Resources/audio/` 现有短音效素材清单，需 Worker 在 Step 1 实查确认。

## 4. Constraints
1. **程序集与运行期铁律**：
   - `Presentation/UI` 可依赖 `Logic`/`Data`/`Foundation`，但严禁在 `DialoguePanel`/`GamePlayPanel`/新增 HUD 组件中私写 `Update()`（使用 `GameRoot.Instance.RegisterSceneRoot` 实现 `IRenderTicked`，或利用受 `_paused` / `Time.unscaledDeltaTime` 控制的既定驱动/协程）。
   - `GamePlayPanel` 热路径（导航坐标投影、残血计时、摇摆动画）保持 **0 GC**（预先分配池化槽位、禁用 LINQ 与每帧 `new`）。
2. **资源可替换原则**：所有新视觉（水珠导航 Icon、槽位标、浮窗底板）与新音效（打字机短音、弹窗滑音）必须优先尝试读 `AssetModule` / `AudioConfig` / Inspector 序列化字段；程序生成 Sprite / `AudioClip` 仅作缺省兜底，且必须在销毁时正确释放。
3. **配表与脚本冲突避让**：
   - 修改 `ConfigWorkspace/Data/slot_dialogue.xlsx` 已获 CEO 专项授权，但**严禁** `openpyxl` 重建表（遵从 `ConfigWorkspace/AGENTS.md`）；**严禁删除或重排 `paragraph == 1002` 的 `line.Id = 10, 11, 12, 13`**。
   - **Worktree 合并秩序**：若批 1 尚未合入主线，本批先在隔离 Worktree 完成代码/Prefab/文案草改；Luban 全量镜像导出与 `Tools/comment-budget.json` 最终修改必须基于批 1 合入后的最新表状态再执行一次，避免覆盖批 1 的 4 张表。
4. **注释预算守卫**：清理 `AudioManager.cs` 至预算 `<= 718`；对预算为 `0`/`21` 的 `DialoguePanel.cs`、`AudioConfig.cs`、`GamePlayPanel.cs` 保持零/极少注释（必要 9 类说明放在新建的 `PopupMotion.cs` / `HudHintBanner.cs` / `SupplyNavIndicator.cs` 并压在 `5.37` 密度线内），仅允许 `<summary>` / `<remarks>`。

## 5. Proposed Design

### 5.1 打字机扩展与音效系统（`Typewriter.cs` + `AudioConfig.cs` + `AudioManager.cs` + `SfxPlayback.cs`）
1. **`Assets/Scripts/Presentation/UI/Component/Typewriter.cs`**：
   - 暴露 `public bool IsPlaying => _running != null;`。
   - 新增 `Play(TMP_Text text, float charsPerSecond, Action onCharRevealed = null, Action onCompleted = null)` 重载（兼容既有 2 参签名）。
   - 在 `CoReveal` 中：仅当 `shown > prevShown` 且新揭示字符非空白（跳过纯空格/换行，且 `textInfo.characterCount` 已自动剔除 TMP 富文本标签）时触发一次 `onCharRevealed?.Invoke()`；自然结束与 `Complete()` 时触发 `onCompleted?.Invoke()`（`Stop()` 手动中断不误触发完成）。
   - 新增静态工具方法 `CountVisibleCharacters(TMP_Text text)` 或估算读取时长辅助函数（基于实际 `textInfo.characterCount` 而非含 `<color=...>` 的原始字符串长度）。
2. **`Assets/Scripts/Data/Settings/AudioConfig.cs` & `AudioManager.cs` & `SfxPlayback.cs`**：
   - 在 `AudioConfig.cs`（Step 1 先读原文件字段）增加可选字段（如 `typewriterBlipKey`、`popupSlideInKey`、`popupSlideOutKey` 或可选 `AudioClip` 直连槽，全部留空默认不报错）及 `typewriterBlipCooldown`（默认 `0.055f`）、`uiSfxVolumeScale`（默认 `0.65f`）。
   - 在 `AudioManager.cs`：
     - `Init()` 时按配置 Key 尝试加载真实 `AudioClip`；未配或加载不到时，使用 `AudioClip.Create` 一次性生成并缓存短促温和的程序占位音（**“哒哒哒”**：~40ms、~720Hz 正弦/三角带指数衰减短促木鱼/按键音；**滑入**：~95ms 轻快上扬软音；**滑出**：~80ms 轻柔下行收音），并在 `Dispose()` 中仅销毁程序自建 Clip（AssetModule 加载的走 `Release`）。
     - 新增专用非阻塞接口 `PlayUiBlip()`（绕过普通战斗音效的同 Clip 去重拦截，自带 `_nextBlipUnscaledTime` 节流防高频刺耳，并做轻微 `0.94~1.06` pitch 抖动或使用专用复用/短占 `AudioSource`，不抢占 `PriorityBgm = 0`）与 `PlayUiSlide(bool isEntering)`。
     - 将 `AudioManager.cs` 既有超标注释精简回 `<= 718` 预算内。

### 5.2 弹窗边滑入滑出 + 跷跷板摇动 + 长度驱动退出（`DialoguePanel.cs` + `PopupMotion.cs` + `StoryFlow.cs`）
1. **新建 `Assets/Scripts/Presentation/UI/Component/PopupMotion.cs`（纯 Presentation UI 动画器，0 GC）**：
   - 绑定到每个 Popup 根对象（`ImgNPCPopup`、`ImgPlayerPopup`，兼容其子文本跟随移动与旋转）。
   - `Awake/CaptureBasePose()` 记录预制体原本的 `anchoredPosition`、`localRotation`（绝不写死坐标）。
   - 边缘滑向：根据静止位置 `anchoredPosition.x` 自动判定从左屏外或右屏外（距离 = `rect.width + slideMargin`，可 Inspector 配置）滑入（`unscaledDeltaTime`，Cubic Ease-Out，约 `0.26s`），滑出沿原路退出（Cubic Ease-In，约 `0.22s`），配合 `CanvasGroup` 同步淡入淡出（由 `PopupMotion` 与 `UIFader` 协同）。
   - **跷跷板摇动**：显示期间（非 `_paused`）驱动 `localRotation = _baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * wobbleHz * 2f * Mathf.PI) * wobbleAngle)`（默认 `wobbleAngle = 2.0°`、`wobbleHz = 1.1f`，Inspector 可调）；隐藏完成立刻复位旋转与锚点位置。
2. **`Assets/Scripts/Presentation/UI/Panel/DialoguePanel.cs`**：
   - 为 `Npc`/`Machine` 对话框接入 `Typewriter` 的 `onCharRevealed -> PlayUiBlip()`（点按跳全时停止发声）。
   - 为 `NpcPopup`/`PlayerPopup` 配备独立 `_popupTypewriter` 与 `PopupMotion`，实现：`ShowPopup` 启动滑入 + 滑入音效 + 摇摆 + 逐字渐出“哒哒哒”；`HidePopupContent` 启动滑出 + 滑出音效；提供 `IsPopupTyping` 只读属性以及 `SetPaused(paused)` 同步冻结两路打字机与摇摆滑行。
   - **Prefab 层级核查兜底**：Step 1 查 `DialoguePanel.prefab` 中 `TxtNPCPopup` 是否在 `ImgNPCPopup` 子节点下；若为兄弟节点则让 `PopupMotion` 同步带动文本 `RectTransform` 或在 Step 1 中规范挂接。
3. **`Assets/Scripts/Presentation/Story/StoryFlow.cs`**：
   - 取消固定 `PopupDurationSeconds = 3f`；改为：
     - 参数：`MinPopupHoldSeconds = 3.2f`、`PostTypewriterHoldSeconds = 2.0f`、`MaxPopupHoldSeconds = 10f`、`PopupCharsPerSecond = 24f`（或从 `DialoguePanel` 读取统一速度）。
     - 在 `ShowNextPopup()`：调用 `DialoguePanel.ShowPopup` 获取可见字符数（富文本剔除后）计算 `duration = Mathf.Clamp(visibleChars / charsPerSecond + PostTypewriterHoldSeconds, MinPopupHoldSeconds, MaxPopupHoldSeconds)`，排入 `Timer.Schedule(duration, ...)`。
     - 在 `RenderTick` 收尾检查：除了等待 `_popupExpired`，再增加守卫若 `_panel != null && _panel.IsPopupTyping` 仍为真（例如极端低帧率导致逐字尚未吐完），延后至逐字结束再隐藏，确保**退出时机严格不早于文字播放完毕**。

### 5.3 HUD 槽位键位标 + 水珠导航 + 轻量支援/残血浮窗（`GamePlayPanel.cs` + 2 个新组件）
1. **槽位键位标（`GamePlayPanel.cs` + `GamePlayPanel.prefab`）**：
   - Step 1 检查 `GamePlayPanel.prefab` 槽位布局，在 `ImgWaterBG`、`ImgEarthBG`、`ImgSeedBG` 对应角标位置增加 `LMB`、`RMB`、`E`（并在持有种子时可选附带 `Q 丢弃` 微型提示）文本/图标槽；若预制体尚未放置，由 Worker 在 `GamePlayPanel.prefab` 补建规范节点（或代码自愈兜底创建），文本与字体走 `BasePanel` 既定 `TMP` 风格，后续可直接在 Prefab 上一键替换成键鼠美术图标。
2. **屏幕边缘水珠导航图标（`Assets/Scripts/Presentation/UI/Component/SupplyNavIndicator.cs`）**：
   - 由 `GamePlayPanel`（通过注册 `GameRoot` `IRenderTicked` 或 `BasePanel` 驱动协程——严禁使用独立 `Update()`）每渲染帧推进（暂停/非 `Running` 状态冻结或隐藏）。
   - 读取 `CombatRoot.Current?.Fountains`；每个泉眼对应一个池化 UI 水滴项：
     - 使用主相机 `WorldToViewportPoint` 判定：若在屏内安全框 `[0.06..0.94, 0.08..0.92]` 内则平滑隐藏；若在屏外则把方向向量夹到屏幕边距矩形，并将图标锚定至该边缘点、旋转使其水滴尖端指向泉眼方向！
     - 配色按 `Fountain.Kind` 区分（水蓝 / 土琥珀 / 生命翠绿，残血时生命水珠微弱呼吸提醒）。
     - Sprite 加载顺序：先试 `AssetModule.Load<Sprite>($"Icons/Nav_{kind}")` 或 Prefab 序列化 override -> 缺失则复用静态缓存的**程序化生成平滑水滴 `Sprite`**（64×64 抗锯齿 SDF 水滴贴图，仅生成 1 次，0 GC）。
3. **独立轻量 HUD 浮窗（`Assets/Scripts/Presentation/UI/Component/HudHintBanner.cs`）**：
   - 挂载/自愈在 `GamePlayPanel` 顶部或侧边安全区，**完全独立于 `StoryFlow`/`DialoguePanel`**，永不暂停游戏、永不锁输入、永不与剧情抢占通道。
   - 采用队列或优先级替换 + 滑入滑出 + 轻柔提示音（可复用 `AudioManager.PlayUiSlide`）：
     - **种子配给提醒**：订阅 `EventBus<WavePhaseChanged>`，当 `evt.Phase == WavePhase.Prep && evt.GrantSeed != SeedType.None` 时显示支援通知（如 `“补给支援：【火种子】已送达！按 E 砸向地面引爆元素反应”`；若检测玩家当前已持有其他种子则提示“已放置于脚边，按 Q 丢弃旧种即可拾取”）。
     - **长时间残血智能提醒**：订阅 `EventBus<PlayerHealthChanged>` 记录血量比；仅在 `GameState.Running` 且 `Current > 0 && Current <= 2`（40%）且所有 `FountainKind.Life` 泉眼的 `PlayerInside == false` 时累计计时（离开残血或进泉清零）；持续达阈值（如 `8.5s`）弹出友好提醒（`“生命危急！沿绿色水滴指引前往【生命泉】停下回血”`），每次触发进入 `30s` 冷却（每关限提示 2–3 次防刷屏），在教学关开场 `StoryFlow` 对话锁期间自动静默不打扰。

### 5.4 引导文案通俗化重构（`ConfigWorkspace/Data/slot_dialogue.xlsx`）
- 保持原有所有 `id`、`paragraph`、`type`、`sfx` 结构不变（严禁破坏 `1002` 段 `id=10/11/12/13` 镜头索引）。
- 逐行通俗化重写 `context` 列（完整文案建议详见执行前确认清单，核心口径如下）：
  - **1001（开局操作）**：简明说明世界观背景后，明确提示 `WASD` 移动、`Shift 或 Space` 冲刺。
  - **1002（三泉介绍）**：将 `能量溶剂提取器` -> `水井`、`荒原基质提取器` -> `土丘`、`活性重构仪` -> `生命泉`；用通俗动作提示“靠近补给”、“左键砸水球、右键砸土块——水砸土、或砸到地形上就能变出泥浆和反应！”。
  - **1003（教学接敌）**：改掉“似乎能伤害虫子”的误导，明确告知“试着朝虫子脚下的空地或元素地形砸水/土球！”。
  - **2001 / 3001（一二关）**：明确提示“生命泉内停下脚步即可恢复（站定越久回得越快，短走动不会前功尽弃）”，鼓励利用窑炉火池等环境地形砸出高伤蒸汽/陶砖。
  - **4001 / 4002 / 5001 / 5002 / 6001（后期）**：`原素结晶` -> `元素种子`，更正“精英怪被击败必掉随机种子”，说明“按 `E` 把种子砸进地面造出元素核心，再用水/土砸它引爆强力反应；按 `Q` 可丢弃手中种子”。

## 6. File Scope

### READ
- `AGENTS.md`、`ConfigWorkspace/AGENTS.md`、`AI_Context/BUILD_AND_TEST.md`
- `Assets/Scripts/Data/Settings/UIConfig.cs`、`Assets/Scripts/Data/Settings/VisualPalette.cs`
- `Assets/Scripts/Data/Config/ConfigModule.cs`、`Assets/Scripts/Data/Config/Spec/SeedSpec.cs`、`Assets/Scripts/Data/Config/Spec/AudioMapSpec.cs`、`Assets/Scripts/Data/Config/Spec/DialogueSpec.cs`
- `Assets/Scripts/Logic/Event/Events.cs`、`Assets/Scripts/Logic/Services/Timer/TimerManager.cs`
- `Assets/Scripts/Presentation/Root/GameRoot.cs`、`Assets/Scripts/Presentation/Root/CombatRoot.cs`（只读 `Fountains` 口，**禁改**）、`Assets/Scripts/Presentation/World/Fountain.cs`（只读属性，**禁改**）
- `Assets/Scripts/Presentation/UI/UIMgr.cs`、`Assets/Scripts/Presentation/UI/BasePanel.cs`、`Assets/Scripts/Presentation/UI/Panel/HudPanel.cs`
- `Assets/Resources/ui/`（检视 `DialoguePanel` 与 `GamePlayPanel` 预制体结构）、`Assets/Resources/config/UIConfig.asset`、`Assets/Resources/audio/`（盘点现有短音频文件）
- `Assets/StreamingAssets/Luban/dso_tbslotdialogue.json`、`Assets/StreamingAssets/Luban/dso_tbaudiomap.json`
- `Assets/Tests/Runtime/Editor/音频策略Tests.cs`、`HUD受击反馈Tests.cs`、`测试夹具.cs`

### CREATE
- `Assets/Scripts/Presentation/UI/Component/PopupMotion.cs`
- `Assets/Scripts/Presentation/UI/Component/SupplyNavIndicator.cs`
- `Assets/Scripts/Presentation/UI/Component/HudHintBanner.cs`
- `Assets/Tests/Runtime/Editor/引导与UI反馈Tests.cs`（须登记进 `Tools/comment-budget.json`）

### MODIFY（含 CEO 专项授权项）
- `Assets/Scripts/Presentation/UI/Component/Typewriter.cs`
- `Assets/Scripts/Presentation/UI/Component/UIFader.cs`
- `Assets/Scripts/Presentation/UI/Panel/DialoguePanel.cs`
- `Assets/Scripts/Presentation/Story/StoryFlow.cs`
- `Assets/Scripts/Data/Settings/AudioConfig.cs`
- `Assets/Scripts/Presentation/Audio/AudioManager.cs`
- `Assets/Scripts/Presentation/Audio/SfxPlayback.cs`
- `Assets/Scripts/Presentation/UI/Panel/GamePlayPanel.cs`
- `Assets/Resources/config/AudioConfig.asset` *(CEO 授权修改 SO)*
- `Assets/Resources/ui/` 下对应的 `DialoguePanel.prefab` 与 `GamePlayPanel.prefab` *(Step 1 读取 `UIConfig` 锁定精确路径后在白名单内更新；CEO 授权修改 UI Prefab)*
- `ConfigWorkspace/Data/slot_dialogue.xlsx` *(CEO 授权改表，禁脚本重建工作表)*
- Luban 导表输出区（仅经导表镜像生成，**须在批 1 合入后再执行**）：`Assets/Scripts/Generated/Config/**`、`Assets/StreamingAssets/Luban/**`
- `Tools/comment-budget.json` *(清理 `AudioManager.cs` 至预算内或同步登记 `引导与UI反馈Tests.cs`；合入时与批 1/2 合并条目)*

### FORBIDDEN（并行冲突防线）
- **严禁修改批 1 文件**：`SceneObstacleBaker.cs`、`NavField.cs`、`TileObstacleDirector.cs`、`TilemapAdapter.cs`、`LifeFountainState.cs`、`Fountain.cs`、`TileGridLightmap.cs`、`EnemyTuning.asset`、`VisualFeelTuning.asset`、`enemy.xlsx`、`tile_state.xlsx`、`element_rule.xlsx`、`element_duo_reaction.xlsx`
- **严禁修改批 2 文件**：`PlayerStats.cs`、`PlayerController.cs`、`CombatRoot.cs`
- **严禁修改**：`TimerWheel.cs`、`SaveSlot.cs`、`InputSys.cs`、`Packages/manifest.json`、`ProjectSettings/**`
- **严禁删除或修改** `slot_dialogue.xlsx` 中 `paragraph == 1002` 的行号 `10, 11, 12, 13` 映射语义。

## 7. Execution Steps
1. **Preflight & 资产结构盘点**：
   - 检查分支/Worktree 与 `ConfigWorkspace/AGENTS.md`、`UIMgr.cs`、`BasePanel.cs`、`AudioConfig.cs`。
   - 核实 `Assets/Resources/ui/` 中 `DialoguePanel` 与 `GamePlayPanel` 预制体精确路径、子节点关系（尤其 `TxtNPCPopup` 与 `ImgNPCPopup` 归属、`ImgWaterBG`/`ImgEarthBG`/`ImgSeedBG` 锚点布局与挂载字体）；盘点 `Assets/Resources/audio/` 是否有可复用的 UI 短音素材。
2. **扩展 `Typewriter` 与 `AudioManager`（打字机回调 + 可替换程序短音兜底）**：
   - 实现 `Typewriter.Play(..., onCharRevealed, onCompleted)`、`IsPlaying`、`CountVisibleCharacters`；
   - 在 `AudioConfig.cs` 扩展可选 UI 音效 Key/Clip 与节流参数；在 `AudioManager.cs` 实现加载优先 + 程序合成短促音兜底、`PlayUiBlip()`（含 `0.055s` 节流与防同 Clip 去重屏蔽）及 `PlayUiSlide(bool)`；同时压减 `AudioManager.cs` 超标旧注释至 `<= 718`。
   - 验证：编译通过，且 `音频策略Tests` 与新“哒哒哒”节流测试全绿。
3. **实现 `PopupMotion`、更新 `DialoguePanel` 与 `StoryFlow`**：
   - 实现 `PopupMotion`（边缘滑入滑出 + 持续轻柔跷跷板摇动 + 暂停冻结 + 销毁/隐藏复位）。
   - 在 `DialoguePanel` 对对话框与弹窗双通道接入打字机“哒哒哒”音效、Popup 滑动音效与摇摆；注意保持 `DialoguePanel.cs` 注释字符数 `== 0`（把技术说明放在 `PopupMotion.cs` 密度上限内）。
   - 更新 `StoryFlow.ShowNextPopup()` 按可见字符数计算停留时长，并在 `RenderTick` 确保逐字未播完前不提前退出；验证 `Play/Pause/Save/Restore` 状态切换不破坏弹窗显示与相机偏移。
4. **实现 `GamePlayPanel` 键位标、`SupplyNavIndicator` 与 `HudHintBanner`**：
   - 在 `GamePlayPanel.prefab`（及代码兜底）为水/土/种子三槽补齐 `LMB`、`RMB`、`E`（持种提示 `Q`）标识。
   - 实现 `SupplyNavIndicator`（读 `CombatRoot.Current?.Fountains`，屏幕外边缘钳位+指向旋转+水/土/生命分色，优先加载 `Icons/Nav_*`、缺失走程序生成平滑水滴 Sprite）。
   - 实现 `HudHintBanner`（订阅 `WavePhaseChanged` 发配给支援通知；按 `PlayerHealthChanged` 与 `LifeFountain.PlayerInside` 计时发残血回血智能提醒，不阻塞 `StoryFlow`）。
   - 注册到 `GameRoot.Instance` `IRenderTicked`（或遵循 `UIMgr` 统一驱动），严禁私写 `Update()`；确保 `GamePlayPanel.cs` 注释 `<= 21`。
5. **通俗化改写 `slot_dialogue.xlsx` 并同步导表**：
   - 保留 `id / paragraph / type / sfx`（严守 `1002` 段 `10..13`），更新通俗易懂文案；
   - **重要**：待批 1 合入 `VReason/Update` 后基于合入提交运行一次完整 Luban 导出（避免覆盖批 1 的 4 张玩法表）与 `pwsh Tools/luban-mirror.ps1`。
6. **编写 EditMode 测试并全量过门禁**：
   - 创建 `引导与UI反馈Tests.cs` 并登记进 `Tools/comment-budget.json`；
   - 运行 `pwsh Tools/compile-gate.ps1 -Topology`、`pwsh Tools/comment-lint.ps1`、`pwsh Tools/luban-mirror.ps1`、Unity EditMode Test Runner 与 `Level_0`~`Level_3` Play Mode 全流程验收。

## 8. Engineering Rules
1. 严禁新增任何业务脚本的 `Update()` / `FixedUpdate()`；HUD 每帧逻辑必须由 `GameRoot` (`IRenderTicked`) 或统一受控驱动推进。
2. UI 热路径（边缘投影、摇摆旋转、低血计时）保持 **0 GC**（预先建好水珠 UI 池与组件引用缓存，不每帧 `Find` / `GetComponent` / 拼接字符串）。
3. 严禁硬编码替代可换装路径：程序生成的占位水滴图与合成音效仅作为 `AssetModule` / `AudioConfig` 未命中时的兜底，且必须在 `OnDestroy`/`Dispose` 时完整销毁释放，不泄漏纹理与 `AudioClip`。
4. `DialoguePanel.cs`（预算 0）、`AudioConfig.cs`（预算 0）、`GamePlayPanel.cs`（预算 21）严禁超预算加注释；`AudioManager.cs` 须在本批修回预算 `<= 718`；新文件严格控制在 `5.37` 字符/代码行以内。
5. 严禁使用 `openpyxl` 重建 `slot_dialogue.xlsx`。

## 9. Acceptance Criteria
- `AC-01`: `pwsh Tools/compile-gate.ps1 -Topology` 输出 `0 Error(s)`、`0 Warning(s)`，拓扑守卫全部通过。（证据：CLI 日志 `AI_Context/EVIDENCE/GD-2026-0003/`）
- `AC-02`: `pwsh Tools/luban-mirror.ps1` 待删除、待覆盖、孤儿 `.meta` 均为 `0`；`slot_dialogue` 去除了晦涩专有名词且保留了 `1002` 段 `10..13` 镜头动作。（证据：`dso_tbslotdialogue.json` Diff + `Level_0` Play 实测）
- `AC-03`: 打字机与“哒哒哒”音效：对话框与弹窗均逐字显示且触发清脆可辨的节流“哒哒哒”短音，跳字/暂停/隐藏时声音即刻停止且不压住 BGM；`AudioConfig` 可直接指定替换音频资源。（证据：`引导与UI反馈Tests` + Play 录屏）
- `AC-04`: 弹窗动效与动态停留时长：弹窗从屏幕边缘平滑滑入/滑出并伴随轻微音效，停留期间轻微跷跷板摇动；长文本弹窗停留显著长于短文本且在逐字播完后才退出；暂停时动画与计时同步冻结。（证据：`引导与UI反馈Tests` + `Level_0`/`Level_1` 录屏）
- `AC-05`: HUD 槽位键位标：`GamePlayPanel` 水/土/种子槽位清晰可见 `LMB`、`RMB`、`E`（及持种 `Q`）标识，不遮挡数字且在各分辨率下对齐。（证据：`Level_1` HUD 截图）
- `AC-06`: 水珠边缘导航：泉眼在屏内时不干扰画面；泉眼出屏后，屏幕边缘出现对应颜色（水蓝/土黄/生命绿）水滴图标指向泉眼方向，跟随相机移动平滑转向；替换 `Icons/Nav_*` 即可换图。（证据：`引导与UI反馈Tests` 投影计算用例 + `Level_1` Play 截图）
- `AC-07`: 轻量支援与残血提示：波前配给触发时 HUD 轻量提示条告知支援来源，且与 `StoryFlow`（如 `Level_3` 第 2 波）同时发生时不报错不遮挡；残血且未在生命泉持续达阈值后触发回血提醒，进泉或回血后重设且不高频刷屏。（证据：`引导与UI反馈Tests` + `Level_3` Play 验证）
- `AC-08`: 注释门检查：`AudioManager.cs`、`DialoguePanel.cs`、`GamePlayPanel.cs`、新组件及测试文件全部通过 `pwsh Tools/comment-lint.ps1` 检查（合入批 1 后全仓除未改的 `TimerWheel.cs`/`SaveSlot.cs` 外全绿）。（证据：`comment-lint.ps1` 输出）

## 10. Test Plan
> 依据 `AI_Context/BUILD_AND_TEST.md`；无统一 CLI EditMode 测试入口，测试于 Unity Test Runner GUI 运行并留存日志截图。

1. **V1 — 静态与注释门**（工作目录：工程根）：
   - `pwsh Tools/comment-lint.ps1` -> 核对本批改动及新增文件零违规，`AudioManager.cs` 不再超预算。
2. **V2 — 编译与拓扑门**（工作目录：工程根）：
   - `pwsh Tools/compile-gate.ps1 -Topology` -> `0 Error(s)`、`0 Warning(s)`、四类守卫全部通过。
3. **导表与镜像门**（批 1 合入后执行）：
   - Luban 导出 + `pwsh Tools/luban-mirror.ps1` -> 待删除 `0`、待覆盖 `0`、孤儿 `.meta` `0`。
4. **V3 — EditMode 自动测试**（Unity Editor GUI）：
   - `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`（重点：`引导与UI反馈Tests`、`音频策略Tests`、`HUD受击反馈Tests`、`Data层Tests`、`EventBusTests`）。
5. **V4 — 实机全流程验收**（Unity Play Mode）：
   - `Assets/Scenes/Level_0.unity`：从新游戏完整走完教学关，确认文案通俗、镜头依次聚焦三泉（`1002` 未破坏）、弹窗渐出+哒哒哒+滑入摇摆+够时长读完、`LMB/RMB/E` 槽标清晰、`Shift/Space` 冲刺提示正确。
   - `Assets/Scenes/Levels/Level_1.unity` & `Level_3.unity`：
     - 跑出泉眼视野外确认屏幕边缘水珠图标正确出现并指向三泉；
     - 压低血量并在生命泉外走动观察智能残血回血提示触发及冷却；
     - 在 `Level_3` 推进至第 1、第 2 波 `Prep`（同时有 `StoryFlow` `4002` 与种子配给），验证支援浮窗与剧情系统共存无异常；
     - 途中按 `Esc` 暂停再恢复，验证打字机、摇摆、计时与音效均准确暂停与续播。

## 11. Risks
1. **R2 — `DialoguePanel.prefab` 节点结构未知风险**：若 `TxtNPCPopup` 与 `ImgNPCPopup` 在预制体里并非父子层级，仅移动/旋转 `ImgNPCPopup` 会导致文字留在原地。*缓解*：Step 1 首要核查预制体层级并同步绑定/移动两者或整理父子层级。
2. **R2 — 多 Worktree Luban 导表覆盖冲突**：批 1 与批 3 均触碰 `ConfigWorkspace/Data/*.xlsx` 并生成 `StreamingAssets/Luban/`。*缓解*：批 3 代码与 `slot_dialogue.xlsx` 先开发，待批 1 合入主线后再拉取最新主线执行导表并合入。
3. **R1 — `Typewriter` 与高刷/掉帧“哒哒哒”过噪**：若每字无节流直接放音会形成嗡鸣。*缓解*：`AudioManager.PlayUiBlip` 硬节流（`>= 0.055s`）+ 温和短音包络 + pitch 微调 + 暂停静音。
4. **R1 — `Canvas` 缩放模式差异导致边缘导航偏移**：`UIMgr` Canvas 若为 `Screen Space - Camera` 或带 `CanvasScaler` 非 1:1 缩放，直接用屏幕像素写 `anchoredPosition` 会位置错偏。*缓解*：Step 1 读取 `UIMgr.cs` 确认 Canvas 渲染模式，统一用 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 换算边缘局部坐标。

## 12. Rollback
1. 在独立 Worktree 分支 `ai/GD-2026-0003` 施工；如方案需回退，直接回滚或弃用该分支，不影响 `VReason/Update` 主干或批 1/批 2 成果。
2. 若 `slot_dialogue.xlsx` 或 UI Prefab 发生异常，同时还原 `.xlsx`、Prefab 与对应 `StreamingAssets/Luban/dso_tbslotdialogue.json` 并重跑 `pwsh Tools/luban-mirror.ps1`。

## 13. Open Questions
> 非阻塞，Worker Step 1 必查验证项。
1. `Assets/Resources/ui/` 中 `DialoguePanel.prefab` 与 `GamePlayPanel.prefab` 的精确文件名、节点结构及 `UIMgr.cs` 所用 `Canvas` 渲染模式为何？
2. `Assets/Resources/audio/` 中现存 `s_01..s_38` 是否已有干净短促的界面点击音可复用/裁切；若无或尾巴过长，直接使用程序合成的轻量缓存 `AudioClip`（后续由音效师在 `AudioConfig` 替换）是否符合预期？
3. 本批计划把 `Typewriter` 速度默认设为 ~24 字/秒、弹窗最小停留 `3.2s` + 播完留 `2.0s` 阅读缓冲，正式手测后微调参数通过 `DialoguePanel` / `StoryFlow` 序列化字段调整。

## 14. Approval
*(由 CEO 审阅并填写至 `APPROVAL_RECORD.md`，Planner 不得预先代填。)*
- 批准 Plan 版本：
- 批准人 / 时间：
- 专项授权确认（`slot_dialogue.xlsx`、UI Prefab、`AudioConfig.asset`、`Tools/comment-budget.json`、Worktree 接力合入）：
- 附加条件或拒绝项：
--- END OF TASK ---
