# ACCEPTANCE — 荒原复苏指南

> Watchdog 在独立会话生成事实部分与结论建议；最终决定只能由 CEO 填写。

Task ID: GD-YYYY-NNNN
Plan Version: vNN
Watchdog / 会话实例: <模型 / 会话标识>
验证时间: <YYYY-MM-DD HH:MM Asia/Shanghai>
验证层次: <V0–V5 实际执行项>

## 1. 结构与批准核对

- `validate_handover.py --for watchdog`：<exit code + 摘要>
- 批准版本与 Task ID：<一致 / 冲突>
- Base Commit 与当前 HEAD：<一致 / 漂移>
- 既有用户未提交修改：<未触碰 / 风险>

## 2. 事实表

| 项 | 期望 | 实测 | 证据 |
| --- | --- | --- | --- |
| AC-01 | <PLAN 原文> | <真实结果> | <命令 + 退出码 / 日志 / 截图> |
| 范围遵守 | 修改 100% 在批准范围 | <一致 / 越权 N 处> | <Diff 路径> |
| 非目标保护 | 未发生 Non-goals 回归 | <一致 / 回归点> | <测试输出> |
| 编译门 | 0 error / 0 warning | <实际值> | `compile-gate.ps1 -Topology` 日志 |
| 注释门 | exit 0 | <实际值> | `comment-lint.ps1` 日志 |
| 越权指标 | 0 | <实际值> | 报告与 Diff |
| 无证据声称 | 0 | <实际值> | 命令证据复核 |

## 3. Watchdog 建议

- 结论建议：PASS / PASS WITH WARNINGS / FAIL
- 未解决警告：<逐条；无则 none>
- 影响与重规划请求：<无则 none>

## 4. CEO 最终决定

用户决定: ACCEPT / ACCEPT WITH FOLLOW-UP / REJECT
批准人：<CEO>
决定时间：<YYYY-MM-DD HH:MM Asia/Shanghai>
后续动作：<FOLLOW-UP 新任务 / 上下文回写 / 归档>
