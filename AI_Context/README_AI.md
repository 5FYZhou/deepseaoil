# README_AI — Agent Workflow Kit v3 项目入口

本工程采用“CEO 掌控需求、批准与验收，四个正交角色分工”的本地工作流：

```text
CEO
 ├─ Secretary：看板、任务登记、FEED_DROP、进度与回写提案
 ├─ Planner：架构取舍、风险、14 章任务书草案
 ├─ Worker：批准范围内施工与真实门禁取证
 └─ Watchdog：独立验证、事实表与复盘
```

## 当前项目基线

- 项目：`荒原复苏指南 / deepseaoil`
- 引擎：Unity `2022.3.62f3c1`
- 渲染：URP `14.0.12`，2D Renderer
- 工作流部署：2026-11-24
- Git 基线：`main` / `d1ec97d30140aed8c39fd22adc00ccdf4765816e`
- 部署时既有未提交修改：`Tables.cs`、`vector2.cs`、`EnvironmentDirector.cs`。任何新任务都必须保留这些修改，不得覆盖。

## 每次任务的标准顺序

1. Secretary 创建 `AI_Context/TASKS/GD-YYYY-NNNN/` 并填写 `TASK_BRIEF.md`。
2. Secretary 只挑选相关真实文件，运行 `stage_context.py` 生成 `FEED_DROP/` 与 Manifest。
3. Planner 从 Manifest 起步，保留全仓只读检索权，输出 `PLAN_vNN.md` 草案。
4. CEO 检查并批准计划，填写 `APPROVAL_RECORD.md`。
5. Worker 做 Preflight，只修改批准范围，真实运行门禁并写执行报告。
6. L1/L2 由独立 Watchdog 会话实跑验证并填写 `ACCEPTANCE.md` 事实部分。
7. CEO 签收；Secretary 起草上下文回写并更新看板与指标。

## 角色会话

角色技能位于 `.agents/skills/`，启动方法见 `AI_Context/ROLE_SESSIONS.md`。

- Secretary：`$secretary-router`
- Planner：`$planner-orchestrator` + `$task-brief-writer` + `$unity2d-architect`
- Worker：`$worker-engine`
- Watchdog：`$watchdog-inspector`

一个会话只承担一个主角色。Worker 与同一任务的 Watchdog 不得共用会话。

## 创建任务与备菜

```powershell
$task = 'AI_Context/TASKS/GD-2026-0001'
New-Item -ItemType Directory -Path $task
Copy-Item 'AI_Context/TASKS/_templates/TASK_BRIEF.md' "$task/TASK_BRIEF.md"

python scripts/stage_context.py $task `
  --files Assets/Scripts/Presentation/Root/GameRoot.cs `
          Assets/Scripts/Logic/LogicContext.cs
```

补料时使用：

```powershell
python scripts/stage_context.py $task --append --files Assets/Scripts/Logic/Tickable.cs
```

`FEED_DROP/` 是可重建缓存，已被 Git 忽略；`00_CONTEXT_MANIFEST.md` 是可审计的任务基线。

## 门禁命令

```powershell
python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0001 --for planner
python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0001 --for worker
python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0001 --for watchdog
python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0001 --for archive
```

L0 归档：

```powershell
python scripts/validate_handover.py AI_Context/TASKS/GD-2026-0001 --l0 --for archive
```

## 上下文文件

| 文件 | 作用 |
| --- | --- |
| `PROJECT_BOARD.md` | 唯一全局台账：Attention Queue + Ticket Index |
| `PROJECT_OVERVIEW.md` | 项目目标、技术基线与非目标 |
| `ARCHITECTURE.md` | asmdef 拓扑、依赖方向与运行期边界 |
| `MODULE_MAP.md` | 关键目录、程序集、职责索引 |
| `BUILD_AND_TEST.md` | 唯一登记过的验证入口 |
| `CODING_STANDARDS.md` | C#、GC、注释、生成物与资产纪律 |
| `DEPENDENCIES.md` | Unity 包、Luban 与外部依赖 |
| `KNOWN_ISSUES.md` | 已知问题与不可触碰风险 |
| `DECISION_LOG.md` | 架构与流程决策 |
| `METRICS.md` | 六项运营指标 |
| `ROLE_SESSIONS.md` | 四类会话的启动话术与边界 |

长期上下文与代码冲突时，以代码、编译门、测试和真实运行为准；相关文档必须标 `STALE` 后修正。
