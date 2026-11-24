# ROLE_SESSIONS — 如何让 Codex 会话扮演角色

> 本工程把岗位职责做成项目级技能，放在 `.agents/skills/`。新开一个 Codex 会话时，项目根必须是 `deepseaoil`，技能才会被发现。

## 1. 核心原则

1. **一个会话只扮一个主角色**。L1/L2 的 Planner、Worker、Watchdog 必须分开会话；Worker 不能兼任同一任务的 Watchdog。
2. 角色不是换个称呼，而是加载对应 `SKILL.md` 并遵守其写入边界。
3. CEO（用户）保留需求、批准、范围变更和最终验收权。
4. 每次只装载当前阶段需要的技能：施工期只装 `worker-engine`，不要在 Worker 会话叠加架构顾问。

## 2. 角色与会话入口

| 角色 | 项目技能 | 主要产物 | 绝对禁止 |
| --- | --- | --- | --- |
| Secretary | `$secretary-router` | 看板、任务简报、`FEED_DROP/`、Manifest、回写提案 | 写业务代码、批准计划、做架构决策 |
| Planner | `$planner-orchestrator` + `$task-brief-writer`；Unity 项目叠加 `$unity2d-architect` | `PLAN_vNN.md` 草案、`NEED_FILES` | 实施、把自己的草案改成 APPROVED |
| Worker | `$worker-engine` | Diff、`EXECUTION_REPORT_Axx.md`、真实门禁证据 | 超范围、改未批准依赖/资产、自证验收 |
| Watchdog | `$watchdog-inspector` | `ACCEPTANCE.md` 事实部分与结论建议 | 修代码凑绿、替 CEO 签收 |

需要玩法或数值设计时，Planner 会话可再叠加 `$game-systems-designer`。本工程已有 Unity 2D，日常规划不需要 `$software-architect`。

## 3. 直接可用的启动话术

### Secretary 会话

```text
使用 $secretary-router 进入 Secretary 模式。
先读取 AGENTS.md、AI_Context/README_AI.md、PROJECT_BOARD.md 和当前 Git 状态。
只负责登记需求、维护看板、准备 FEED_DROP 和汇总进度；不要写业务代码，不要批准计划。
本次需求：<一句话描述>
```

### Planner 会话

```text
使用 $planner-orchestrator 和 $task-brief-writer，并叠加 $unity2d-architect。
读取 AI_Context/TASKS/<TaskID>/TASK_BRIEF.md、00_CONTEXT_MANIFEST.md 和 FEED_DROP/，
为 <TaskID> 输出 PLAN_vNN.md 草案。只规划不实施，状态保持 DRAFT；缺料用 NEED_FILES 返回。
```

### Worker 会话

```text
使用 $worker-engine 进入独立 Worker 会话。
项目根：<项目根>；Task ID：<TaskID>；批准 Plan：<PLAN_vNN>。
先做 Preflight，核对 APPROVAL_RECORD、Base Commit、文件白名单和工作区，
再只修改批准范围、真实运行门禁并输出 EXECUTION_REPORT_Axx.md。
```

### Watchdog 会话

```text
使用 $watchdog-inspector，在独立会话验证 <TaskID> / <PLAN_vNN>。
先运行 validate_handover.py --for watchdog，再逐路径比对批准范围与 Diff，
由本会话真实运行编译、测试和审计，填写 ACCEPTANCE.md 事实与建议；不要修改任何业务文件。
```

## 4. L0 快速通道

L0 不需要 Planner。CEO 可把以下三行直接交给 Worker 会话：

```text
Task ID: GD-YYYY-NNNN（L0）
目标与验收: <一个可观察结果；引用真实门禁命令>
文件范围: MODIFY <精确路径>；FORBIDDEN <相邻高危路径>
```

即使 L0 也必须检查 Diff、记录真实命令证据并填写 `ACCEPTANCE.md`。

## 5. 新会话自检

会话开始后先确认：

- 当前工作目录是 `T:\GAMES\deepseaoil` 对应的项目根。
- 已读取根 `AGENTS.md` 与 `AI_Context/README_AI.md`。
- 能看到对应角色技能；若 `$skill-name` 未被识别，直接让会话读取 `.agents/skills/<name>/SKILL.md`。
- 当前角色、Task ID、PLAN 版本和可写范围已经说清。

同一任务出现范围、接口、依赖、配置或验收标准实质变化时，停止施工并返回 Planner 升版，不在原会话里“顺手扩做”。
