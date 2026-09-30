// ---------------------------------------------------------------------------
// 框架设计文档 · 一致性自检测试
//
// 守护的是「图不会悄悄漂移」这件事：蓝图里的 mermaid 代码块是渲染副本，
// Docs/框架设计/sources/*.mmd 才是可编辑源。只改一边就会红。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.EditorTools.Tests
{
    public class 框架文档一致性Tests
    {
        // ================================================================
        // 路径与常量
        // ================================================================

        static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }
        static string DesignDir { get { return Path.Combine(ProjectRoot, "Docs", "框架设计"); } }

        const string 蓝图 = "框架蓝图.md";
        const string 设计文档 = "Data 层设计.md";
        const string 实现文档 = "Data 层实现.md";

        /// <summary>考古/对照性质的行内标记：这些行不是"旧命名残留"，是刻意做的变更记录。</summary>
        static readonly string[] 对照行标记 = { "草案", "→", "被否决" };

        /// <summary>个别例外：技术注释里点名旧实现的名字，属于必要说明而非残留。</summary>
        static readonly string[] 对照行例外 = { "不是 Newtonsoft 的" };

        /// <summary>该行是否落在「考古章节」内：H2 标题含「草案」或「相对旧版」的整节。</summary>
        static bool 在考古章节内(string[] lines, int index)
        {
            for (int i = index; i >= 0; i--)
                if (lines[i].StartsWith("##"))
                    return lines[i].Contains("草案") || lines[i].Contains("相对旧版");

            return false;
        }

        static string ReadAll(string path)
        {
            // 统一成 LF，避免 CRLF/LF 混用造成的假失败
            return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        static string[] Lines(string text) { return text.Split('\n'); }

        /// <summary>
        /// 取出蓝图里所有 mermaid 代码块（按出现顺序）。
        /// 图源目录与 mmdc 渲染流程已取消（见 §10.4），**图块本身就是唯一副本**。
        /// </summary>
        static List<string> 蓝图图块()
        {
            var md = ReadAll(Path.Combine(DesignDir, 蓝图));
            var blocks = new List<string>();

            foreach (Match m in Regex.Matches(md, "```mermaid\\n(?<body>[\\s\\S]*?)```"))
                blocks.Add(m.Groups["body"].Value.TrimEnd('\n'));

            return blocks;
        }

        static string 设计文档全文()
        {
            var sb = new StringBuilder();
            foreach (var f in new[] { 蓝图, 设计文档, 实现文档 })
                sb.Append(ReadAll(Path.Combine(DesignDir, f)));
            return sb.ToString();
        }

        // ================================================================
        // T1 · 已删除的图源/渲染产物不再被引用
        // ================================================================

        [Test]
        public void T1_不再引用已删除的图源与图片产物()
        {
            // `Docs/框架设计/sources/` 与 mmdc 渲染流程已取消——图块本身就是唯一副本（见蓝图 §10.4）。
            // 本测试守的是"删干净了"：三份文档里不得再留下指向外部图源或渲染产物的引用。
            // 注意这里是**正则**：字面量 "sources/" 会命中 "Resources/"（前者是后者的子串），
            // 必须用前置断言排除掉。这条坑是写完先跑一遍才发现的。
            var banned = new[] { @"(?<![A-Za-z])sources/", "mmdc", "Docs/images", "Docs/sources" };

            var hits = new List<string>();

            foreach (var f in new[] { 蓝图, 设计文档, 实现文档 })
            {
                var lines = Lines(ReadAll(Path.Combine(DesignDir, f)));

                for (int i = 0; i < lines.Length; i++)
                {
                    // 考古/对照行放行：那里本来就要提"旧机制叫什么"
                    if (对照行标记.Any(w => lines[i].Contains(w))) continue;
                    if (在考古章节内(lines, i)) continue;

                    foreach (var b in banned)
                        if (Regex.IsMatch(lines[i], b))
                            hits.Add(string.Format("{0}:{1} 仍匹配「{2}」", f, i + 1, b));
                }
            }

            Assert.IsEmpty(hits, "文档仍在引用已删除的图源 / 图片产物：\n  " + string.Join("\n  ", hits));
        }

        // ================================================================
        // T2 · 每张图都由「图 N｜」标题引导，且块体非空
        // ================================================================

        [Test]
        public void T2_图块由编号标题引导且非空()
        {
            var md = ReadAll(Path.Combine(DesignDir, 蓝图));
            var lines = Lines(md);

            var blocks = 蓝图图块();
            Assert.GreaterOrEqual(blocks.Count, 8, "蓝图的 mermaid 块少于 8 个，疑似被误删");

            foreach (var b in blocks)
                Assert.IsTrue(b.Trim().Length > 0, "蓝图里存在空的 mermaid 块");

            // 每个 ```mermaid 上方最近的一个标题，必须是「图 N｜…」
            int seen = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("```mermaid")) continue;

                string heading = null;
                for (int j = i - 1; j >= 0; j--)
                    if (lines[j].StartsWith("##")) { heading = lines[j]; break; }

                Assert.IsNotNull(heading, "第 " + (i + 1) + " 行的 mermaid 块上方找不到标题");
                Assert.IsTrue(Regex.IsMatch(heading, @"图 \d+｜"),
                    "mermaid 块上方的标题不含「图 N｜」：" + heading);
                seen++;
            }

            Assert.AreEqual(blocks.Count, seen, "标题计数与图块计数不一致");
        }

        // ================================================================
        // T3 · 无旧命名残留
        // ================================================================

        [Test]
        public void T3_无旧命名残留()
        {
            // 每条：被禁的字符串 + 判定原因
            var banned = new[]
            {
                new { Text = "Jam.Config",      Why = "生成物落在默认程序集 Assembly-CSharp，不存在 Jam.Config 程序集" },
                new { Text = "Jam.Data",        Why = "命名空间统一为 DeepseaOil.Data" },
                new { Text = "JamRoot",         Why = "工程名是 deepseaoil，命名空间前缀 DeepseaOil" },
                new { Text = "Newtonsoft",      Why = "Luban 生成目标是 cs-simple-json，Loader 用 Luban.SimpleJSON" },
                new { Text = "JObject",         Why = "同上" },
                new { Text = "Config 配置层",   Why = "该层已改名 Data 数据层" },
                new { Text = "复位三项",        Why = "切场景复位是四项（多了 AssetModule.OnSceneSwitch）" },
                new { Text = "两级程序集",      Why = "工程实际有 4 个 asmdef，蓝图已改为事实描述" },
            };

            var hits = new List<string>();

            foreach (var f in new[] { 蓝图, 设计文档, 实现文档 })
            {
                var path = Path.Combine(DesignDir, f);
                var lines = Lines(ReadAll(path));

                // 白名单只放"考古/对照"性质的内容：H2 标题带「草案」「相对旧版」的整节，
                // 行内含「草案」「→」「被否决」的对照行，以及点名旧实现名字的技术注释。
                // 除此之外的正文一律扫描——那才是会误导实现者的地方。
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (对照行标记.Any(w => line.Contains(w))) continue;
                    if (对照行例外.Any(w => line.Contains(w))) continue;
                    if (在考古章节内(lines, i)) continue;

                    foreach (var b in banned)
                        if (line.Contains(b.Text))
                            hits.Add(string.Format("{0}:{1} 出现「{2}」（{3}）", f, i + 1, b.Text, b.Why));
                }
            }

            Assert.IsEmpty(hits,
                "框架设计文档里出现已废弃的表述：\n  " + string.Join("\n  ", hits));
        }

        // ================================================================
        // T4 · 交叉引用有效
        // ================================================================

        [Test]
        public void T4_交叉引用有效()
        {
            var md = ReadAll(Path.Combine(DesignDir, 蓝图));

            // 文档内引用的两份 Data 层文档（Markdown 链接，路径含空格与百分号编码）
            foreach (var doc in new[] { 设计文档, 实现文档 })
            {
                var encoded = doc.Replace(" ", "%20");
                var referenced = md.Contains(doc) || md.Contains(encoded);
                Assert.IsTrue(referenced, "蓝图没有引用 " + doc);
                Assert.IsTrue(File.Exists(Path.Combine(DesignDir, doc)), "被引用的文档不存在：" + doc);
            }
        }

        // ================================================================
        // T5 · 契约表齐全
        // ================================================================

        [Test]
        public void T5_契约表齐全()
        {
            var md = ReadAll(Path.Combine(DesignDir, 蓝图));

            var section = Regex.Match(md, "##\\s*11\\.\\s*重要契约(?<body>[\\s\\S]*?)(\\n##\\s|$)");
            Assert.IsTrue(section.Success, "蓝图里找不到「## 11. 重要契约」一节");

            var body = section.Groups["body"].Value;
            var rows = body.Split('\n').Count(l => l.TrimStart().StartsWith("|"));
            Assert.GreaterOrEqual(rows, 20, "契约表行数不足（含表头与分隔行应 ≥ 20，当前 " + rows + "）");

            Assert.IsFalse(body.Contains("复位三项"), "契约表里还有「复位三项」，应为四项");
            Assert.IsTrue(body.Contains("复位四项"), "契约表里缺少「复位四项」");
        }

        // ================================================================
        // T6 · 生成物目录里没有手写文件
        // ================================================================

        [Test]
        public void T6_生成物目录无手写文件()
        {
            // 基线：Luban 实际生成的 11 个 .cs（加表时同步这份清单）
            var generated = new HashSet<string>(StringComparer.Ordinal)
            {
                "Tables.cs",
                "vector2.cs",
                "vector3.cs",
                "vector4.cs",
                "demo/Fish.cs",
                "demo/Item.cs",
                "demo/Quality.cs",
                "demo/TbFish.cs",
                "demo/TbItem.cs",
                "demo/TbWeapon.cs",
                "demo/Weapon.cs",
            };

            var dir = Path.Combine(Application.dataPath, "Scripts", "Config");
            Assert.IsTrue(Directory.Exists(dir), "生成物目录不存在：" + dir);

            var unknown = new List<string>();
            foreach (var f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var rel = f.Substring(dir.Length + 1).Replace('\\', '/');
                if (!generated.Contains(rel)) unknown.Add(rel);
            }

            Assert.IsEmpty(unknown,
                "Assets/Scripts/Config/ 是生成物专用目录，导表时整目录镜像覆盖。发现非生成物文件（手写物请放 Scripts/Framework/ 或 Scripts/Game/）：\n  "
                + string.Join("\n  ", unknown));
        }

        // ================================================================
        // T7 · Data 层文件清单与文档同源
        // ================================================================
        //
        // 「文档即规格」的机械守卫：Data 层实现文档 §1 的文件树是清单，
        // 这里把它解析出来逐个断言磁盘上真有这个文件。
        // 树格式变了会解析出 0 条 → 断言失败并提示格式，而不是静默通过。

        [Test]
        public void T7_Data层文件清单与文档同源()
        {
            var md = ReadAll(Path.Combine(DesignDir, 实现文档));

            var section = Regex.Match(md,
                "##\\s*1\\.\\s*文件清单与依赖顺序(?<body>[\\s\\S]*?)(\\n##\\s|$)");
            Assert.IsTrue(section.Success, "实现文档里找不到「## 1. 文件清单与依赖顺序」一节");

            var block = Regex.Match(section.Groups["body"].Value, "```[a-zA-Z]*\\n(?<tree>[\\s\\S]*?)```");
            Assert.IsTrue(block.Success, "§1 里找不到文件树的代码块");

            // 树的缩进单位是 4 字符（"├── " / "│   "），nameStart / 4 = 深度
            var TreeChars = new[] { ' ', '│', '├', '└', '─' };
            var stack = new List<KeyValuePair<int, string>>();   // depth → 累积路径（目录带尾斜杠）
            var files = new List<string>();

            foreach (var raw in Lines(block.Groups["tree"].Value))
            {
                if (raw.Trim().Length == 0) continue;

                int nameStart = 0;
                while (nameStart < raw.Length && Array.IndexOf(TreeChars, raw[nameStart]) >= 0) nameStart++;
                if (nameStart >= raw.Length) continue;

                int depth = nameStart / 4;
                var rest = raw.Substring(nameStart);
                var name = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];

                if (depth > stack.Count)
                {
                    Assert.Fail(string.Format("§1 文件树缩进跳级（depth {0} > 已累积 {1}）：{2}", depth, stack.Count, raw));
                }

                while (stack.Count > depth) stack.RemoveAt(stack.Count - 1);

                var parent = depth == 0 ? string.Empty : stack[depth - 1].Value;

                if (name.EndsWith("/"))
                {
                    stack.Add(new KeyValuePair<int, string>(depth, parent + name));
                }
                else if (name.EndsWith(".cs"))
                {
                    files.Add(parent + name);
                }
            }

            Assert.GreaterOrEqual(files.Count, 15,
                "§1 文件树只解析出 " + files.Count + " 个 .cs，少于预期的 15 个：检查文件树格式是否变了");

            var missing = files.Where(f => !File.Exists(Path.Combine(ProjectRoot, f.Replace('/', Path.DirectorySeparatorChar)))).ToArray();

            Assert.IsEmpty(missing,
                "Data 层实现文档 §1 声明了这些文件，但磁盘上没有（文档与代码脱钩）：\n  " + string.Join("\n  ", missing));
        }

        // ================================================================
        // T8 · 切场景第 ④ 项复位已接线
        // ================================================================
        //
        // T5 只断言契约表**写着**「复位四项」；这条断言代码里真的调了第 ④ 项，
        // 否则「四项」就只是一句话。

        [Test]
        public void T8_切场景第四项复位已接线()
        {
            var path = Path.Combine(ProjectRoot, "Assets", "Scripts", "Framework", "Logic", "Services", "SceneService.cs");
            Assert.IsTrue(File.Exists(path), "找不到 SceneService.cs：" + path);

            var src = ReadAll(path);

            Assert.IsTrue(src.Contains("AssetModule.OnSceneSwitch"),
                "SceneService.Load 里没有调用 AssetModule.OnSceneSwitch —— 契约表说的「复位四项」只有三项落地");

            int iSwitch = src.IndexOf("AssetModule.OnSceneSwitch", StringComparison.Ordinal);
            int iLoad = src.IndexOf("LoadScene(", StringComparison.Ordinal);

            Assert.GreaterOrEqual(iLoad, 0, "SceneService 里找不到 LoadScene 调用");
            Assert.Less(iSwitch, iLoad,
                "AssetModule.OnSceneSwitch 出现在 LoadScene 之后 —— 顺序反了，复位必须在换场景之前");
        }
    }
}
