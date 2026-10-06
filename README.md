# 深海鱼油？嗯！

Unity 2022.3.62f3c1 + URP 14.0.12。三层架构（数据层 / 逻辑层 / 表现层），配表走 Luban。
俯视角 2D：投掷（水球 / 土球）→ 落地改格子 → 格子对踩上去的敌人结算伤害，外加水球掉落物与波次敌人。

## 先读哪份

| 想知道什么 | 看哪份 |
| :-- | :-- |
| 每个文件夹装什么、哪里不能动 | [`Docs/目录说明.md`](Docs/目录说明.md) |
| 分层、每帧顺序、跨层契约、程序集、已知缺陷 | [`Docs/框架设计/框架蓝图.md`](Docs/框架设计/框架蓝图.md) |
| 某一层怎么设计 | [`Docs/框架设计/分层设计/`](Docs/框架设计/分层设计/) |
| 从空白场景重新接一遍线（含 Cinemachine 相机） | [`Docs/框架设计/新场景接线.md`](Docs/框架设计/新场景接线.md) |
| 谁依赖谁、一次调用怎么走（16 张图） | [`Docs/框架设计/架构图集.md`](Docs/框架设计/架构图集.md) |
| 战斗切片（格子 / 球 / 掉落物 / 敌人 / HUD）各件职责与落地链路 | [表现层 §8](Docs/框架设计/分层设计/表现层.md) ＋ [逻辑层 §7](Docs/框架设计/分层设计/逻辑层.md) |
| 怎么改表、导表怎么排障 | [`Docs/表格数据配置/`](Docs/表格数据配置/) |
| 特效怎么播、怎么加一个 | [`Docs/粒子特效系统/`](Docs/粒子特效系统/) |
| 某条决定当初为什么这么定、落地了没有 | [`Docs/审查.md`](Docs/审查.md)（带落项标注的决策记录） |

## 代码在哪

```
Assets/Scripts/
├── Foundation/      与游戏无关的地基（单例基类、对象池、随机、Y 排序、状态机骨架、弹道数学）
├── Data/            数据层：数值配置（Luban）＋ 调参 SO ＋ 资源加载 ＋ 观测面
├── Logic/           逻辑层：纯 C#，不碰 UnityEngine
│                    （Actor / Movement / Player / Input / Bounds / Event / Services
│                      ＋ 战斗切片的 Combat / Grid / Projectile / Drop / Wave）
├── Presentation/    表现层：组合根（GameRoot / PlayerController / CombatRoot）、适配器、战斗切片各件、UI、特效
└── Generated/       🔴 机器生成，禁手改
```

## 跑起来

- 打开 `Assets/Scenes/` 里的场景进 Play。战斗切片的验收入口是 `EmptyTest.unity`（它接了 `GameRoot` / `PlayerController` / `CombatRoot` / `TilemapAdapter` / `InputProvider` / `Fountain`）。
- **进 Play 后是菜单状态（`timeScale = 0`，一切冻结）**，点开始面板的 `StartBtn` 才进 `Running`。
- 导表：菜单 **Luban ▸ 表格数据导入**（`Ctrl/Cmd+Shift+D`）。
- 测试：`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`（当前 132 条；其中 `Assets/Tests/Tools/` 那 14 条走 `DeepseaOil.EditorTools.Tests` 程序集）。
- 只想验投掷链路：`CombatRoot` 的 Inspector 上把「是否刷敌人」取消勾选即可（此时不会有敌人，但格子、球、掉落物、喷泉照常跑）。
- 编译门（不打开 Unity 也能查编译）：`pwsh Tools/compile-gate.ps1`。
