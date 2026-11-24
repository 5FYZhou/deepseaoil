---
name: task-brief-writer
description: v3.0 工程任务书撰写插件。供 Planner 在 PLAN 阶段加载，产出 0.Metadata + 14 章、START/END OF TASK 包裹、FEED_DROP/Manifest 引用、精确文件白名单、可判定 AC 和 Worker/Watchdog 交接信息。只写计划，不写工程实现。
---

# Role: 工程任务书撰写器

## 一、核心定位

任务书是 Planner 交给 CEO 审批、再交给 Worker 执行的唯一正式接口。它必须让有能力的 Worker 在不再追问产品和架构的情况下完成 Preflight，同时让 Watchdog 能逐条验证。

本技能只负责结构、完整性和可判定性。技术取舍、风险级别和模型路由来自 Planner 与 `AGENTS.md`。

## 二、包裹协议

跨会话或单贴派发时，完整使用：

```text
--- START OF TASK ---
（0.Metadata + 14 章）
--- END OF TASK ---

### 取舍与背景说明
（只给 CEO/Planner 审阅，Worker 忽略）
```

包裹线内不得混入闲谈、备选方案过程或未决问题。单文件 `PLAN_vNN.md` 可不需要包裹线，但正文仍须自足。

## 三、固定骨架

```markdown
# AI ENGINEERING TASK SPECIFICATION

## 0. Metadata
- Task ID: GD-YYYY-NNNN
- Plan Version: v01
- Status: DRAFT
- Task Level: L0–L3 / Risk Level: R0–R3
- Project / Engine / Platform:
- Branch / Base Commit: 或可恢复副本时间
- Planner: 模型 + 模式 + 档位
- Worker: 模型 + Agent 模式 + 档位
- Watchdog: 所需独立实例 / 模型 + 档位
- Context: 00_CONTEXT_MANIFEST.md + Base Commit
- Execution Attempt Cap: A01–A03
- Created At:

## 1. Objective
## 2. Non-goals
## 3. Current State
## 4. Constraints
## 5. Proposed Design
## 6. File Scope
## 7. Execution Steps
## 8. Engineering Rules
## 9. Acceptance Criteria
## 10. Test Plan
## 11. Risks
## 12. Rollback
## 13. Open Questions
## 14. Approval
```

状态只能由 Planner 写 `DRAFT` / `NEED_INVESTIGATION`。CEO 批准后可改为 `APPROVED`，并由独立 `APPROVAL_RECORD.md` 锁定版本和文件范围。

## 四、章节质量标准

### 1 Objective

描述可观察结果和完成定义，不写“优化一下”“做得更好”等模糊词。

### 2 Non-goals

明确最容易顺手做但本次不批准的事项，包括相邻重构、依赖升级、资产变更、公共接口扩展和无关格式化。

### 3 Current State

每条事实标记 VERIFIED / OBSERVED / INFERRED / UNKNOWN / STALE。引用 `FEED_DROP` 文件时必须给出原路径映射。

### 4 Constraints

写硬约束：语言、引擎、平台、性能、资源生命周期、数据兼容、线程/异步、安全、离线限制和不可改目录。

### 5 Proposed Design

给职责、数据流、接口、失败行为与最小替代方案；不要写伪代码式“以后补”。

### 6 File Scope

精确列出：

- READ
- CREATE
- MODIFY
- FORBIDDEN

目录通配只在能证明边界安全时使用；相邻的高危配置、资产、公共接口和生成文件必须列入 FORBIDDEN。

### 7 Execution Steps

每步写前置条件、操作路径、预期结果和检查方式。步骤应让 Worker 能按 A01–A03 熔断。

### 8 Engineering Rules

约束资源释放、错误传播、输入校验、生命周期、序列化、GC、线程与日志；禁止吞异常、删断言、改测试迎合实现和无关重构。

### 9 Acceptance Criteria

写 `AC-01..AC-NN`，每条可判 `PASS / FAIL / NOT RUN / NOT APPLICABLE`，并明确证据位置。

### 10 Test Plan

只引用项目 `BUILD_AND_TEST.md` 已登记且本环境真实可跑的命令；逐项标工作目录和期望。无法运行的环境门禁必须标 `NOT RUN`，不得预先写 PASS。

### 11 Risks

列影响、概率、触发条件、缓解和回滚。R2+ 必须有专项验证与分阶段点。

### 12 Rollback

给基线、逆向步骤、数据/资产恢复和验证；不依赖覆盖用户未提交修改。

### 13 Open Questions

阻塞问题在 CEO 批准前解决；非阻塞假设写明验证步骤，不用“视情况而定”。

### 14 Approval

Planner 留空给 CEO。批准内容包括 Plan 版本、允许文件、条件、禁止项、批准人和时间；不得由 Planner 预先代填。

## 五、FEED_DROP 与 NEED_FILES

计划 Metadata 引用 `00_CONTEXT_MANIFEST.md`，不引用旧 Markdown 上下文包。

若当前暂存不足：

```text
NEED_FILES
- path/to/file
理由：该文件将决定哪个接口、调用方或验证命令
```

Secretary 补拷后重新审阅 Manifest；Chat 模式与 Agent 模式必须消费同一物理投喂入口。

## 六、L0 mini 任务书

L0 不使用 14 章，但保留三行：

```markdown
Task ID: GD-YYYY-NNNN（L0）
目标与验收: 一个可观察结果；验收 = 明确命令或现象
文件范围: MODIFY path；FORBIDDEN 相邻高危项
```

L0 如涉及公共接口、序列化、配置、资产删除、依赖或跨模块变化，立即升级 L1。

## 七、分批与冲突

- 单批必须能由一个 Worker 会话独立完成。
- 强依赖任务按拓扑拆分，每批写前置 Task ID。
- 多批并行前建立“批次 × 文件”冲突矩阵；同一文件不能分给并行 Worker。
- 接线与语义重构分开，后者单独回归验证。
- 文件、接口、依赖、风险或 AC 实质变化必须升版重审。

## 八、DoD

- [ ] 目标、Non-goals、File Scope 与 AC 完全一致。
- [ ] 每个拟改文件可追溯到具体执行步骤。
- [ ] 每条 AC 和测试有可观察证据。
- [ ] Metadata 明示 Worker、Watchdog、Manifest 与尝试上限。
- [ ] 计划正文可单贴自足，不依赖对话记忆。
- [ ] 不含模板占位符、未决伪选项或臆造命令。
- [ ] `--for planner` 可验证的结构条件已满足。
