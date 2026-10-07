// ---------------------------------------------------------------------------
// 仓库一致性 · 行为测试
//
// 只守两件「错了会静默出事」的事：
//
//   1. 生成物目录里混进了手写文件
//      Assets/Scripts/Generated/ 会被整目录镜像覆盖 —— 导表覆盖 Config/，
//      重新导入 .inputactions 覆盖 Input/。放进去的手写文件下次必丢，且不报错。
//
//   2. Docs/目录说明.md 与磁盘脱钩
//      目录文档一旦腐烂就会骗人（旧版的 Data 层实现文档里粘贴的代码副本
//      已经漂移 47 行没人发现，就是因为当时只断言「文件存在」不比对内容）。
//      这条把「文档里写的每个路径都真实存在」变成机器可判。
//
// 【刻意删掉的一类断言】文档文本格式检查
// （旧 T1 图源残留 / T2 mermaid 图块标题 / T3 旧命名残留 / T4 交叉引用 /
//   T5 契约表行数 / T7 实现文档文件树）。它们断言的是文档的写法而非代码的行为，
//   改一次文档结构就红一片，真正的一致性却守不住。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.EditorTools.Tests
{
    public class 仓库一致性Tests
    {
        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }

        static string ReadAll(string path)
        {
            // 统一成 LF，避免 CRLF/LF 混用造成的假失败
            return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // ================================================================
        // 1 · 生成物目录无手写文件
        // ================================================================

        [Test]
        public void 生成物目录无手写文件()
        {
            // 基线：机器产出的全部 .cs。加表 / 改动作表时同步这份清单。
            var generated = new HashSet<string>(StringComparer.Ordinal)
            {
                // Luban 导表产出（-c cs-simple-json）
                "Config/Tables.cs",
                "Config/vector2.cs",
                "Config/vector3.cs",
                "Config/vector4.cs",
                "Config/demo/BallType.cs",
                "Config/demo/Enemy.cs",
                "Config/demo/Fish.cs",
                "Config/demo/Item.cs",
                "Config/demo/Player.cs",
                "Config/demo/Projectile.cs",
                "Config/demo/Quality.cs",
                "Config/demo/TbEnemy.cs",
                "Config/demo/TbFish.cs",
                "Config/demo/TbItem.cs",
                "Config/demo/TbPlayer.cs",
                "Config/demo/TbProjectile.cs",
                "Config/demo/TbTileInitial.cs",
                "Config/demo/TbTileState.cs",
                "Config/demo/TbWave.cs",
                "Config/demo/TbWeapon.cs",
                "Config/demo/TileInitial.cs",
                "Config/demo/TileState.cs",
                "Config/demo/TileStateType.cs",
                "Config/demo/Wave.cs",
                "Config/demo/Weapon.cs",
                // Input System 从 InputSys.inputactions 生成
                "Input/InputSys.cs",
            };

            var dir = Path.Combine(Application.dataPath, "Scripts", "Generated");
            Assert.IsTrue(Directory.Exists(dir), "生成物目录不存在：" + dir);

            var unknown = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var rel = f.Substring(dir.Length + 1).Replace('\\', '/');
                if (!generated.Contains(rel)) unknown.Add(rel);
            }

            Assert.IsEmpty(unknown,
                "Assets/Scripts/Generated/ 是生成物专用目录，导表与 .inputactions 重新导入都会镜像覆盖它。"
                + "发现非生成物文件（手写物请放 Scripts/Data、Scripts/Logic、Scripts/Presentation、Scripts/Foundation）：\n  "
                + string.Join("\n  ", unknown));
        }

        // ================================================================
        // 2 · Docs/目录说明.md 与磁盘同源
        // ================================================================

        [Test]
        public void 目录说明与磁盘同源()
        {
            var doc = Path.Combine(ProjectRoot, "Docs", "目录说明.md");
            Assert.IsTrue(File.Exists(doc), "找不到目录说明：" + doc);

            var md = ReadAll(doc);

            var section = Regex.Match(md, "##\\s*仓库目录(?<body>[\\s\\S]*?)(\\n##\\s|$)");
            Assert.IsTrue(section.Success, "《目录说明.md》里找不到「## 仓库目录」一节");

            var block = Regex.Match(section.Groups["body"].Value, "```[a-zA-Z]*\\n(?<tree>[\\s\\S]*?)```");
            Assert.IsTrue(block.Success, "「## 仓库目录」里找不到目录树的代码块");

            // 树的缩进单位是 4 字符（"├── " / "│   "），nameStart / 4 = 深度。
            // 深度 0 是顶层条目（Assets/ ConfigWorkspace/ Docs/），没有根标签。
            // 名字取「到第一个连续 2 个以上空格为止」——否则 TextMesh Pro/ 这类含单空格的名字会被截断。
            var TreeChars = new[] { ' ', '│', '├', '└', '─' };
            var stack = new List<KeyValuePair<int, string>>();   // depth → 累积路径（目录带尾斜杠）
            var paths = new List<string>();
            int lineNo = 0;

            foreach (var raw in block.Groups["tree"].Value.Split('\n'))
            {
                lineNo++;
                if (raw.Trim().Length == 0) continue;

                int nameStart = 0;
                while (nameStart < raw.Length && Array.IndexOf(TreeChars, raw[nameStart]) >= 0) nameStart++;
                if (nameStart >= raw.Length) continue;

                int depth = nameStart / 4;
                var rest = raw.Substring(nameStart);

                var gap = Regex.Match(rest, "  +");
                var name = (gap.Success ? rest.Substring(0, gap.Index) : rest).Trim();
                if (name.Length == 0) continue;

                Assert.LessOrEqual(depth, stack.Count,
                    "目录树第 " + lineNo + " 行缩进跳级（depth " + depth + " > 已累积 " + stack.Count + "）：" + raw);

                while (stack.Count > depth) stack.RemoveAt(stack.Count - 1);

                var parent = depth == 0 ? string.Empty : stack[depth - 1].Value;
                var here = parent + name;

                if (name.EndsWith("/")) stack.Add(new KeyValuePair<int, string>(depth, here));

                paths.Add(here);
            }

            // 只断"文档声明的路径都真实存在"（跨产物一致性）。
            // 刻意**不**断"这份文档能解析出多少条路径"——那是文档格式，改一次目录树就会红。
            var missing = new List<string>();
            foreach (var p in paths)
            {
                var full = Path.Combine(ProjectRoot, p.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full) && !Directory.Exists(full)) missing.Add(p);
            }

            Assert.IsEmpty(missing,
                "《目录说明.md》声明了这些路径，但磁盘上没有（文档与仓库脱钩）：\n  "
                + string.Join("\n  ", missing));
        }
    }
}
