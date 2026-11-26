# AI ENGINEERING TASK SPECIFICATION — 荒原复苏指南

> 使用：复制到任务目录并改名 `PLAN_vNN.md`。Planner 只能写 `DRAFT` 或 `NEED_INVESTIGATION`；`APPROVED` 只能由 CEO 修改。

## 0. Metadata

Task ID: GD-YYYY-NNNN
Plan Version: v01
Status: DRAFT
Task Level: L0 / L1 / L2 / L3
Risk Level: R0 / R1 / R2 / R3
Project: 荒原复苏指南 / Unity 2022.3.62f3c1
Platform: <目标平台>
Branch / Base Commit: <分支 / commit>
Planner: <模型与思考档位>
Worker: <模型与思考档位>
Watchdog: <独立会话 / 实例与模型>
Context Manifest: `00_CONTEXT_MANIFEST.md`
Execution Attempt Cap: A01-A03
Created At: <YYYY-MM-DD HH:MM Asia/Shanghai>
Approved By: <留空>
Approved At: <留空>

## 1. Objective

<可观察、可判定的目标；写清完成定义，不用“优化一下”“修好”等模糊词。>

## 2. Non-goals

- <明确不做的事>
- <最容易顺手扩做的相邻改动>

## 3. Current State

- VERIFIED：<已由源码、命令或门禁确认的事实 + 证据位置>
- OBSERVED：<来自用户描述、截图或日志的现象>
- INFERRED：<推导 + 验证方式>
- UNKNOWN：<缺口 + 补料计划>

## 4. Constraints

- 四层依赖：`Presentation → Logic → Data → Foundation`。
- `Logic` 纯 C#；热路径 0 GC；唯一 `GameRoot` 驱动。
- 不修改未批准的 `.prefab / .unity / .asset / .meta / .xlsx`、`ProjectSettings/`、依赖或渲染管线。
- 既有 3 个未提交修改必须保留，不得覆盖。
- <任务特有约束>

## 5. Proposed Design

<职责、数据流、接口、生命周期和错误边界。说明为什么可以放在现有层，不新增无必要抽象。公共接口、asmdef、Serialize 字段、资源加载和事件顺序变化必须显式披露。>

## 6. File Scope

- READ: <路径>
- CREATE: <路径>
- MODIFY: <路径>
- FORBIDDEN: `.prefab`、`.unity`、`.asset`、`.meta`、`.xlsx`、`ProjectSettings/`、`Packages/manifest.json`、未列出的公共接口和依赖
- 用户手工清单：<路径、操作、期望值、判据；无则 none>
- 需重审条件：<出现哪些文件、接口、依赖、资产或 AC 变化必须升版>

## 7. Execution Steps

| # | 前置条件 | 路径 / 位置 | 操作 | 预期结果 | 检查方式 |
| --- | --- | --- | --- | --- | --- |
| 1 | <前置> | <路径> | <动作> | <可观察结果> | <命令 / Diff / 测试> |

## 8. Engineering Rules

- 生命周期：<Awake / OnEnable / Tick / Destroy 时序与所有权>
- 主线程与资源释放：<要求>
- 0 GC：<逐项说明热路径约束>
- Editor 隔离：<是否需要>
- 生成物与序列化：<禁止或兼容要求>
- 测试：不得删除、跳过或弱化既有断言。

## 9. Acceptance Criteria

- AC-01：<可判定 PASS / FAIL / NOT RUN>
- AC-02：<...>
- AC-03：实际修改 100% 位于批准 File Scope。

## 10. Test Plan

- V1：`pwsh Tools/comment-lint.ps1`
- V2：`pwsh Tools/compile-gate.ps1 -Topology`，要求 0 error / 0 warning
- V3：Unity Test Runner → EditMode → Run All；记录通过 / 失败数，未运行写 NOT RUN
- 配表专项（如适用）：`pwsh Tools/luban-mirror.ps1`
- V4：<场景、观察点、截图 / 录屏要求，或 NOT APPLICABLE>
- V5：<Profiler / GC / DrawCall / 内存预算，或 NOT APPLICABLE>

## 11. Risks

| 风险 | 影响 | 概率 | 缓解 |
| --- | --- | --- | --- |
| <风险> | <影响> | 高 / 中 / 低 | <措施> |

## 12. Rollback

- 基线：<Base Commit / 可恢复副本时间>
- 撤销：<逐文件可执行步骤>
- 不得通过覆盖用户未提交修改或重置整仓来回滚。

## 13. Open Questions

- 阻塞：<批准前必须解决>
- 非阻塞假设：<假设 + 验证方式>

## 14. Approval

- Decision: PENDING
- 批准后填写：Plan Version / Approved Scope / Conditions / Explicitly Forbidden / Base Commit / Approver / Approved At
