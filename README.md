# 深海鱼油？嗯！

Unity 2022.3.62f3c1 + URP 14.0.12。三层架构（数据层 / 逻辑层 / 表现层），配表走 Luban。

## 先读哪份

| 想知道什么 | 看哪份 |
| :-- | :-- |
| 每个文件夹装什么、哪里不能动 | [`Docs/目录说明.md`](Docs/目录说明.md) |
| 分层、每帧顺序、跨层契约、程序集、已知缺陷 | [`Docs/框架蓝图.md`](Docs/框架蓝图.md) |
| 某一层怎么设计 | [`Docs/分层设计/`](Docs/分层设计/) |
| 怎么改表、导表怎么排障 | [`Docs/表格数据配置/`](Docs/表格数据配置/) |

## 代码在哪

```
Assets/Scripts/
├── Foundation/      与游戏无关的地基（单例基类、对象池）
├── Data/            数据层：数值配置 + 资源加载 + 观测面
├── Logic/           逻辑层：纯 C#，不碰 UnityEngine
├── Presentation/    表现层：组合根、Unity 适配、UI
└── Generated/       🔴 机器生成，禁手改
```

## 跑起来

- 打开 `Assets/Scenes/SampleScene.unity` 进 Play。
- 导表：菜单 **Luban ▸ 表格数据导入**（`Ctrl/Cmd+Shift+D`）。
- 测试：`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`。
