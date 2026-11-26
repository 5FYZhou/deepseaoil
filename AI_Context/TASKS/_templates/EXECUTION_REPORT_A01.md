# EXECUTION REPORT — 荒原复苏指南

> Worker 每次尝试一份，改名 `EXECUTION_REPORT_A01.md` / A02 / A03。结论只能使用规定枚举。

Task ID: GD-YYYY-NNNN
Plan Version: vNN
Attempt: A01
Worker: <模型 + Agent 模式 + 思考档位>
Execution Time: <YYYY-MM-DD HH:MM Asia/Shanghai>
Unity: 2022.3.62f3c1
Branch / Base Commit: <...>

## 1. Preflight

- 批准计划：<APPROVED + 版本核对>
- 批准记录：<Decision / Scope / Conditions 核对>
- 工作区：<git status 摘要；既有用户修改>
- 分支与 HEAD：<...>
- 文件范围：<逐项映射>
- 工具与环境：<Unity / pwsh / Python>
- Preflight 结论：<PASS / BLOCKED>

## 2. Actual Changes

- Created: <路径；无则 none>
- Modified: <路径；无则 none>
- Deleted: <默认 none；出现即触发重审>
- 范围外修改：<必须为 none>

## 3. Step Results

| # | 步骤 | 结果 | 证据 |
| --- | --- | --- | --- |
| 1 | <对应 PLAN 步骤> | PASS / FAIL / BLOCKED / NOT RUN | <日志 / Diff / 输出> |

## 4. Commands

| 命令 / 操作 | 工作目录 | 工具版本 | 退出码 / 结果 | 日志位置 |
| --- | --- | --- | --- | --- |
| `pwsh Tools/comment-lint.ps1` | 工程根 | PowerShell / 项目脚本 | <n> | <EVIDENCE 路径> |
| `pwsh Tools/compile-gate.ps1 -Topology` | 工程根 | 项目脚本 | <n> | <EVIDENCE 路径> |
| Unity Test Runner EditMode | Unity Editor | 2022.3.62f3c1 | <通过 / 失败 / NOT RUN> | <证据路径> |

## 5. AC Mapping

| AC | 结果 | 证据 |
| --- | --- | --- |
| AC-01 | PASS / FAIL / NOT RUN | <命令 + 退出码 / Diff / 截图> |

## 6. Tests

- 注释门：<exit code + 摘要>
- 编译 / 拓扑：<0 error / 0 warning 或错误摘要>
- EditMode：<通过数 / 总数 / NOT RUN>
- 配表镜像：<0 / 0 / 0 或 NOT APPLICABLE>
- 运行时与视觉：<观察点 / 截图 / NOT RUN>
- 性能：<数据 / NOT APPLICABLE / NOT RUN>

## 7. Usage

- 输入 tokens：<n>
- 输出 tokens：<n>
- 缓存命中：<n>
- 请求数：<n>
- 若客户端无法提供：写 UNKNOWN，不猜。

## 8. Deviations

<计划偏离、新增假设、接口 / 依赖 / 资产 / 范围变化；无则 none。>

## 9. Remaining Issues

<阻塞、警告和未覆盖风险；无则 none。>

## 10. Rollback

<逐文件可执行回滚步骤；不得 reset 整个工作区。>

## 11. Conclusion

Conclusion: PASS / PASS WITH WARNINGS / FAIL / BLOCKED / NOT RUN
所需决策：<CEO 需要决定的事项；无则 none>
