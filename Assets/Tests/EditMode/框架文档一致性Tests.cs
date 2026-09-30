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
        static string SourcesDir { get { return Path.Combine(DesignDir, "sources"); } }

        const string 蓝图 = "框架蓝图.md";
        const string 设计文档 = "Data 层设计.md";
        const string 实现文档 = "Data 层实现.md";

        /// <summary>考古/对照性质的行内标记：这些行不是"旧命名残留"，是刻意做的变更记录。</summary>
        static readonly string[] 对照行标记 = { "草案", "→", "被否决" };

        static string ReadAll(string path)
        {
            // 统一成 LF，避免 CRLF/LF 混用造成的假失败
            return File.ReadAllText(path).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        static string[] Lines(string text) { return text.Split('\n'); }

        /// <summary>去掉 %% 注释行（源文件专用），保留空行与缩进。</summary>
        static List<string> StripMmdComments(string mmdText)
        {
            return Lines(mmdText).Where(l => !l.StartsWith("%%")).ToList();
        }

        static string TrimTrailingBlank(List<string> lines)
        {
            int end = lines.Count;
            while (end > 0 && lines[end - 1].Trim().Length == 0) end--;
            return string.Join("\n", lines.Take(end));
        }

        /// <summary>取蓝图里「源文件：`sources/xxx.mmd`」+ 紧随其后的 mermaid 代码块。</summary>
        static Dictionary<string, string> 蓝图图块()
        {
            var md = ReadAll(Path.Combine(DesignDir, 蓝图));
            var found = new Dictionary<string, string>();

            // [\s\S]*? 允许中间夹标题与引用行；```` 用 \n 锚定，避免匹配到正文里的行内提及
            var matches = Regex.Matches(md,
                "源文件：`sources/(?<file>[0-9A-Za-z\\-]+\\.mmd)`[\\s\\S]*?\\n```mermaid\\n(?<body>[\\s\\S]*?)```");

            foreach (Match m in matches)
            {
                var file = m.Groups["file"].Value;
                var body = m.Groups["body"].Value.Replace("\r\n", "\n").TrimEnd('\n');
                Assert.IsFalse(found.ContainsKey(file), "蓝图里同一个图源出现两次：" + file);
                found[file] = body;
            }

            return found;
        }

        static string 设计文档全文()
        {
            var sb = new StringBuilder();
            foreach (var f in new[] { 蓝图, 设计文档, 实现文档 })
                sb.Append(ReadAll(Path.Combine(DesignDir, f)));
            return sb.ToString();
        }

        // ================================================================
        // T1 · 图源与蓝图逐字符一致
        // ================================================================

        [Test]
        public void T1_蓝图图块与图源逐字符一致()
        {
            var blocks = 蓝图图块();
            Assert.Greater(blocks.Count, 0, "蓝图里一张图都没解析到：检查「源文件：`sources/xx.mmd`」+ 紧随的 mermaid 代码块");

            var problems = new List<string>();

            foreach (var kv in blocks)
            {
                var srcPath = Path.Combine(SourcesDir, kv.Key);
                if (!File.Exists(srcPath)) { problems.Add(kv.Key + "：图源文件不存在"); continue; }

                var expected = TrimTrailingBlank(StripMmdComments(ReadAll(srcPath)));
                var actual = kv.Value;

                if (expected == actual) continue;

                var e = expected.Split('\n');
                var a = actual.Split('\n');
                for (int i = 0; i < Math.Max(e.Length, a.Length); i++)
                {
                    var el = i < e.Length ? e[i] : "<缺行>";
                    var al = i < a.Length ? a[i] : "<缺行>";
                    if (el != al)
                    {
                        problems.Add(string.Format("{0} 第 {1} 行不一致：\n      源文件: {2}\n      蓝图　: {3}",
                            kv.Key, i + 1, el, al));
                        break;   // 每张图只报第一处，够定位即可
                    }
                }
            }

            Assert.IsEmpty(problems,
                "图源与蓝图不一致（改图请先改 sources/*.mmd，再同步蓝图）：\n  " + string.Join("\n  ", problems));
        }

        // ================================================================
        // T2 · 图源与蓝图配对齐全
        // ================================================================

        [Test]
        public void T2_图源与蓝图配对齐全()
        {
            var blocks = 蓝图图块();
            var onDisk = Directory.GetFiles(SourcesDir, "*.mmd")
                                  .Select(Path.GetFileName)
                                  .OrderBy(x => x, StringComparer.Ordinal)
                                  .ToArray();

            var orphanSource = onDisk.Where(f => !blocks.ContainsKey(f)).ToArray();
            var orphanBlock = blocks.Keys.Where(f => !onDisk.Contains(f)).ToArray();

            Assert.IsEmpty(orphanSource, "sources/ 里有图源但蓝图没引用：" + string.Join(", ", orphanSource));
            Assert.IsEmpty(orphanBlock, "蓝图引用了不存在的图源：" + string.Join(", ", orphanBlock));
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

                // 白名单只放"考古/对照"性质的整节：H2 标题里带「草案」或「相对旧版」的章节，
                // 以及行内含「草案」「→」「被否决」的对照行（写法均为「旧 → 新」）。
                // 除此之外的正文一律扫描——那才是会误导实现者的地方。
                var 考古章节 = lines.Where(l => l.StartsWith("##"))
                                    .Select(l => l.TrimStart('#', ' ').Trim())
                                    .Where(t => t.Contains("草案") || t.Contains("相对旧版"))
                                    .ToArray();

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (对照行标记.Any(w => line.Contains(w))) continue;
                    if (考古章节.Any(t => t.Length > 0 && line.Contains(t))) continue;

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
            var missing = new List<string>();

            // 文档内引用的两份 Data 层文档（Markdown 链接，路径含空格与百分号编码）
            foreach (var doc in new[] { 设计文档, 实现文档 })
            {
                var encoded = doc.Replace(" ", "%20");
                var referenced = md.Contains(doc) || md.Contains(encoded);
                Assert.IsTrue(referenced, "蓝图没有引用 " + doc);
                Assert.IsTrue(File.Exists(Path.Combine(DesignDir, doc)), "被引用的文档不存在：" + doc);
            }

            // sources/*.mmd 引用
            foreach (Match m in Regex.Matches(md, "sources/(?<file>[0-9A-Za-z\\-]+\\.mmd)"))
            {
                var f = m.Groups["file"].Value;
                if (!File.Exists(Path.Combine(SourcesDir, f))) missing.Add(f);
            }
            Assert.IsEmpty(missing.Distinct(), "蓝图引用了不存在图源：" + string.Join(", ", missing.Distinct()));
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
            // 基线：Luban 实际生成的 9 个 .cs（加表时同步这份清单）
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
    }
}
