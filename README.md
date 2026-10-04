# 深海鱼油？嗯！

Unity 2022.3.62f3c1 + URP 14.0.12。三层架构（数据层 / 逻辑层 / 表现层），配表走 Luban。
俯视角 2D：投掷（水球 / 土球）→ 落地改格子 → 格子对踩上去的敌人结算伤害，外加水球资源与波次敌人。

## 先读哪份

| 想知道什么 | 看哪份 |
| :-- | :-- |
| 每个文件夹装什么、哪里不能动 | [`Docs/目录说明.md`](Docs/目录说明.md) |
| 分层、每帧顺序、跨层契约、程序集、已知缺陷 | [`Docs/框架设计/框架蓝图.md`](Docs/框架设计/框架蓝图.md) |
| 某一层怎么设计 | [`Docs/框架设计/分层设计/`](Docs/框架设计/分层设计/) |
| 怎么改表、导表怎么排障 | [`Docs/表格数据配置/`](Docs/表格数据配置/) |
| 战斗切片（格子 / 投掷 / 敌人 / HUD）怎么跑、怎么验、遗留什么 | [`Docs/temp/白模迁移任务/迁移完成报告.md`](Docs/temp/白模迁移任务/迁移完成报告.md) |
| 特效怎么播、怎么加一个 | [`Docs/粒子特效系统/`](Docs/粒子特效系统/) |

## 代码在哪

```
Assets/Scripts/
├── Foundation/      与游戏无关的地基（单例基类、对象池）
├── Data/            数据层：数值配置（Luban）＋ 调参 SO ＋ 资源加载 ＋ 观测面
├── Logic/           逻辑层：纯 C#，不碰 UnityEngine
│                    （Movement / Player / Input / Event / Services ＋ 战斗切片的 Combat / Grid / Projectile / Random / Wave）
├── Presentation/    表现层：组合根（GameRoot / PlayerController / CombatRoot）、适配器、UI、特效
└── Generated/       🔴 机器生成，禁手改
```

## 跑起来

- 打开 `Assets/Scenes/` 里的场景进 Play。战斗切片的验收入口是 `EmptyTest.unity`（它接了 `GameRoot` / `CombatRoot` / `GridView` / `Player`）。
- **进 Play 后是菜单状态（`timeScale = 0`，一切冻结）**，点开始面板的 `StartBtn` 才进 `Running`。
- 导表：菜单 **Luban ▸ 表格数据导入**（`Ctrl/Cmd+Shift+D`）。
- 测试：`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`。
- 战斗切片的接线清单、验收清单与回滚点见 [`Docs/temp/白模迁移任务/迁移完成报告.md`](Docs/temp/白模迁移任务/迁移完成报告.md) §4 / §6。

