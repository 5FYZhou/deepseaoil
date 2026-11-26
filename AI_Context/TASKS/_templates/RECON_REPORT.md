# RECON REPORT（只读调查，禁止写入）— 附录 F 模板

> 使用：上下文缺失或超预算时由取材员（只读形态）填写，产出后并入上下文包；不构成执行授权。

Task ID: GD-YYYY-NNNN / 请求人: <CEO 或 Planner> / 调查时间: <YYYY-MM-DD HH:MM 时区>
调查者: <Secretary 只读取材或独立只读 Agent>

## 1. Scope
允许读取的路径与搜索范围（逐条列出）：
- <路径/关键词 1>
- <路径/关键词 2>

## 2. Repo Map Check
MODULE_MAP 核验：<一致 / 漂移>（漂移文件与现状逐条列出）

## 3. Key Files
| 路径 | 一句话职责 |
| --- | --- |
| <路径> | <职责> |

## 4. Interface Facts
接口与依赖事实，每条标 VERIFIED / OBSERVED / INFERRED：
- <事实 1（VERIFIED）>
- <事实 2（INFERRED，验证方式：...）>

## 5. Risks
风险点与疑似坑（引用文件与行号）：
- <风险 1>

## 6. Context Manifest
建议由 `stage_context.py` 物理投喂的真实文件与 Token 预算：
- 固定层：<核对 L1>；模块层：<核对 L2>；代码层：<文件清单与估算>

## 7. Evidence
命令、搜索关键词与输出位置：
- <命令 / 关键词 → EVIDENCE 路径>

去向：由 Secretary 用 `stage_context.py --append` 补拷并更新 Manifest → 回 PLANNING / 原阶段。
本报告不构成执行授权，不得包含任何项目文件修改。
