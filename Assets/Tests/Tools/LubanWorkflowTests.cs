// ---------------------------------------------------------------------------
// 配表工作流 · 自检测试
//
// 对应执行方案 §5.2「Agent 自检验收」六项，全部机器可判：
//   A 参数串完整性   B 进程读法   C 工作目录
//   D 预检分支       E Play 模式保护   F 输出目录约定
// 另加引号规则的边界用例（§2.5 的「必须验证的边界」）+ 无硬编码盘符。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DeepseaOil.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.EditorTools.Tests
{
    public class LubanWorkflowTests
    {
        // ================================================================
        // A · 参数串完整性（§5.2-A）
        // ================================================================

        [Test]
        public void A1_ExportArgv_有全部必需关键字()
        {
            var argv = LubanProject.BuildExportCommand().Argv;
            var all = string.Join(" ", argv);

            Assert.Contains("--conf", argv, "缺 --conf");
            Assert.Contains(LubanProject.LubanConf, argv, "--conf 必须指向推算出的 luban.conf");
            Assert.Contains("-t", argv, "缺 -t");
            Assert.Contains("client", argv, "target 必须是 client");
            Assert.Contains("--strict", argv, "🔴 缺 --strict → 校验失败仍返回退出码 0，整套校验静默失效");
            Assert.Contains("cs-simple-json", argv, "缺代码目标 -c cs-simple-json");
            Assert.Contains("json", argv, "缺数据目标 -d json");

            // 三个 -x
            Assert.IsTrue(all.Contains("outputCodeDir="), "缺 -x outputCodeDir");
            Assert.IsTrue(all.Contains("outputDataDir="), "缺 -x outputDataDir");
            Assert.IsTrue(all.Contains("pathValidator.rootDir="), "缺 -x pathValidator.rootDir");

            // -x 必须成对出现：每个 -x 后面都跟一个 key=value
            Assert.AreEqual(CountOf(argv, "-x"), argv.Count(a => a.StartsWith("outputCodeDir=")
                || a.StartsWith("outputDataDir=") || a.StartsWith("pathValidator.rootDir=")),
                "-x 与 key=value 数量不匹配");
        }

        [Test]
        public void A2_ValidateOnlyArgv_有_f_与_outputSaver_null_且无输出目录()
        {
            var argv = LubanProject.BuildValidateCommand().Argv;
            var all = string.Join(" ", argv);

            Assert.Contains("--strict", argv, "仅校验模式也要 --strict");
            Assert.Contains("-f", argv, "🔴 缺 -f（只校验不产出）");
            Assert.IsTrue(all.Contains("outputSaver=null"), "🔴 缺 -x outputSaver=null");
            Assert.IsTrue(all.Contains("pathValidator.rootDir="), "缺 -x pathValidator.rootDir");
            Assert.AreEqual(0, argv.Count(a => a.StartsWith("outputCodeDir=")),
                "仅校验模式不得带 outputCodeDir");
            Assert.AreEqual(0, argv.Count(a => a.StartsWith("outputDataDir=")),
                "仅校验模式不得带 outputDataDir");
        }

        [Test]
        public void A3_PathValidatorRoot_指向Assets()
        {
            var argv = LubanProject.BuildExportCommand().Argv;
            string x = argv.First(a => a.StartsWith("pathValidator.rootDir="));
            Assert.AreEqual("pathValidator.rootDir=" + Application.dataPath, x,
                "pathValidator.rootDir 必须等于 Assets 的绝对路径");
        }

        // ================================================================
        // B · 进程读法（§5.2-B）
        // ================================================================

        [Test]
        public void B1_导出内核_无同步ReadToEnd_且用异步读()
        {
            string src = ReadOwnSource("LubanImport.cs");
            Assert.Greater(src.Length, 0, "读不到 LubanImport.cs 源码");

            Assert.IsFalse(Regex_Contains(src, @"RedirectStandardOutput\s*=\s*true[\s\S]{0,600}?\.ReadToEnd\(\)"),
                "🔴 出现「重定向 + 同步 ReadToEnd()」组合 → 输出量大时 Unity 死锁");
            Assert.IsTrue(src.Contains("BeginOutputReadLine"), "🔴 缺 BeginOutputReadLine");
            Assert.IsTrue(src.Contains("BeginErrorReadLine"), "🔴 缺 BeginErrorReadLine");
            Assert.IsTrue(src.Contains("OutputDataReceived"), "缺 OutputDataReceived 异步回调");
        }

        [Test]
        public void B2_不用ArgumentList()
        {
            foreach (var file in new[] { "LubanImport.cs", "LubanProject.cs", "Quote.cs" })
            {
                // 🔴 断言必须只看代码，不看注释：这几个文件的注释里特意写了
                //    「不要用 ProcessStartInfo.ArgumentList」及其理由，是文档而非违规。
                string code = StripComments(ReadOwnSource(file));
                Assert.IsFalse(code.Contains("ArgumentList"),
                    "🔴 " + file + " 的代码里用了 ProcessStartInfo.ArgumentList："
                    + ".NET Standard 2.1 下可用性未核实，不可用会直接 CS1061 编译失败。"
                    + "按 Quote.cs 手工拼引号。");
            }
        }

        [Test]
        public void B3_UTF8解码_防中文报错乱码()
        {
            string src = ReadOwnSource("LubanImport.cs");
            Assert.IsTrue(src.Contains("StandardOutputEncoding"), "🔴 未设 StandardOutputEncoding：中文 Windows 上中文报错会乱码");
            Assert.IsTrue(src.Contains("StandardErrorEncoding"), "🔴 未设 StandardErrorEncoding");
        }

        // ================================================================
        // C · 工作目录（§5.2-C）
        // ================================================================

        [Test]
        public void C1_WorkingDirectory_显式设为workspace()
        {
            var export = LubanProject.BuildExportCommand();
            var validate = LubanProject.BuildValidateCommand();
            Assert.AreEqual(LubanProject.Workspace, export.WorkingDirectory,
                "🔴 WorkingDirectory 必须是 workspace：conf 里全是相对路径，CWD 错则基准错");
            Assert.AreEqual(LubanProject.Workspace, validate.WorkingDirectory,
                "🔴 仅校验模式同样要设 WorkingDirectory");
            Assert.IsTrue(Directory.Exists(LubanProject.Workspace), "workspace 目录不存在：" + LubanProject.Workspace);
        }

        // ================================================================
        // D · 预检分支（§5.2-D）
        // ================================================================

        [Test]
        public void D1_预检三分支_各有明确提示()
        {
            string src = ReadOwnSource("LubanProject.cs");
            Assert.IsTrue(src.Contains("缺工具文件"), "缺 lubanDll 的提示缺失");
            Assert.IsTrue(src.Contains("缺配置"), "缺 lubanConf 的提示缺失");
            Assert.IsTrue(src.Contains("缺配置目录"), "缺 workspace 的提示缺失");

            string importSrc = ReadOwnSource("LubanImport.cs");
            Assert.IsTrue(importSrc.Contains("Win32Exception") || importSrc.Contains("catch (Exception"),
                "dotnet 不可达路径未兜底");
            Assert.IsTrue(importSrc.Contains(".NET SDK 8+"), "dotnet 不可达时的提示缺失");
        }

        [Test]
        public void D2_预检在启动进程之前()
        {
            var pre = LubanProject.Precheck();
            Assert.IsTrue(pre.Ok,
                "预检应当通过，实际：" + pre.Message
                + "\n（若这里失败是因为有 ~$Excel 锁文件，请先保存并关闭被占用的表格再跑测试）");

            string src = ReadOwnSource("LubanImport.cs");
            int precheck = src.IndexOf("LubanProject.Precheck()", System.StringComparison.Ordinal);
            int execute = src.IndexOf("Execute(", System.StringComparison.Ordinal);
            Assert.Greater(precheck, 0, "RunExport 里没有调用 Precheck()");
            Assert.Greater(execute, 0, "找不到 Execute");
        }

        [Test]
        public void D3_Excel锁文件_必须检测且只报警不删除()
        {
            // 🔴 需求：Excel 打开 xlsx 时会产生 ~$ 锁文件。实测 Luban 这时**仍能跑成功**
            //    （读的是磁盘上已保存的那版），所以真正的风险不是读表失败，
            //    而是「表还没保存就导入，静默用了旧数据」。改成「存在锁文件就拦下并让人关表格」。
            string src = StripComments(ReadOwnSource("LubanProject.cs"));
            Assert.IsTrue(src.Contains("~$"),
                "🔴 没有检测 ~$ 锁文件（Excel 占用标记）");
            Assert.IsTrue(src.Contains("正被 Excel 占用"),
                "🔴 缺「表格被 Excel 占用」的提示文案");
            Assert.IsTrue(src.Contains("FindExcelLockFiles"),
                "缺 FindExcelLockFiles：锁文件检测必须能在预检和路径自检里复用");
            Assert.IsTrue(src.Contains("DataDir"),
                "锁文件必须去 ConfigWorkspace/Data 下找");

            // 草案附录第 3 条：只检测不删除。脚本不得删除它不理解的文件。
            // "File.Delete" 在代码里查（去掉注释），避免只改了注释就蒙混过关。
            Assert.IsFalse(src.Contains("File.Delete"),
                "🔴 锁文件检测不得删除文件 —— 那是 Excel 的占用标记，属于用户数据");

            // 理由写在注释里，所以这条要查「保留注释」的原文本
            string raw = ReadOwnSource("LubanProject.cs");
            Assert.IsTrue(raw.Contains("绝不自动删除") || raw.Contains("只检测"),
                "注释里要写明「只检测、不自动删除」的理由");
        }

        [Test]
        public void D4_锁文件检测_当前应当无锁且接口可用()
        {
            // 行为断言：正常情况下应当没有锁文件（测试机没开 Excel 时）
            var locks = LubanProject.FindExcelLockFiles();
            Assert.IsNotNull(locks, "FindExcelLockFiles 不应返回 null");

            // 表名换算：~$weapon.xlsx -> weapon.xlsx
            var names = LubanProject.TableNamesOf(new List<string> { "~$weapon.xlsx", "~$item.xlsx" });
            CollectionAssert.AreEqual(new List<string> { "weapon.xlsx", "item.xlsx" }, names);

            // 有锁文件时预检必须失败，且消息里点名具体是哪些表
            if (locks.Count > 0)
            {
                var pre = LubanProject.Precheck();
                Assert.IsFalse(pre.Ok, "有 ~$ 锁文件时预检必须失败（拦下，避免静默使用未保存的旧数据）");
                Assert.IsTrue(pre.Message.Contains("Excel"), "提示里要说明是 Excel 占用");
            }
        }

        // ================================================================
        // M · 菜单与快捷键
        // ================================================================

        [Test]
        public void M1_菜单名与快捷键()
        {
            string src = StripComments(ReadOwnSource("LubanMenu.cs"));

            // 导入项改名。模式同样按字符拼出来，避免断言自己命中自己。
            string menuItemOpen = "MenuItem(" + "\"";
            string oldMenu = "Luban/" + "导表";
            Assert.IsTrue(src.Contains("表格数据导入"),
                "🔴 菜单项应叫「表格数据导入」");
            Assert.IsFalse(src.Contains(menuItemOpen + oldMenu),
                "旧的「导表」菜单项名应当已经不存在");

            // 🔴 %#l 与本机 Unity 自带绑定冲突，必须换掉
            Assert.IsFalse(src.Contains("%#l"),
                "🔴 不要再用 %#l（Ctrl/Cmd+Shift+L）：与 Unity 自带快捷键冲突");
            Assert.IsTrue(src.Contains("%#d"),
                "应改用 %#d（Ctrl/Cmd+Shift+D）");
        }

        // ================================================================
        // E · Play 模式保护（§5.2-E）
        // ================================================================

        [Test]
        public void E1_焦点触发_有Play模式判断与节流与重入判断()
        {
            string src = ReadOwnSource("LubanAutoTrigger.cs");
            Assert.IsTrue(src.Contains("isPlayingOrWillChangePlaymode"),
                "🔴 焦点触发缺 Play 模式判断");
            Assert.IsTrue(src.Contains("LubanImport.Precheck()"),
                "🔴 焦点触发缺「预检不过就不写时间戳」判断");
            Assert.IsTrue(src.Contains("EditorApplication.isCompiling"), "缺「正在编译则跳过」判断");
            Assert.IsTrue(src.Contains("EditorApplication.focusChanged"), "未订阅 focusChanged");
            Assert.IsTrue(src.Contains("EditorPrefs"),
                "节流计时必须存 EditorPrefs：static 字段会在域重载后丢失，节流会自己失效");
            Assert.AreEqual(30.0, LubanAutoTrigger.CooldownSeconds, "节流应为 30 秒");
        }

        [Test]
        public void E2_焦点触发的处理器签名_必须匹配focusChanged的委托类型()
        {
            // 🔴 这条是被真实的 CS0123 打出来的：
            //    focusChanged 是 Action<bool>，处理器写成 Action（无参）会编译失败：
            //    error CS0123: No overload for 'OnFocusChanged' matches delegate 'Action<bool>'
            //    「名字存在」不等于「签名对得上」——之前就是这么漏掉的，所以这里断言到参数形状。
            var ev = typeof(EditorApplication).GetEvent("focusChanged");
            Assert.IsNotNull(ev, "EditorApplication.focusChanged 不存在");
            var invoke = ev.EventHandlerType.GetMethod("Invoke");
            Assert.IsNotNull(invoke, "拿不到 focusChanged 的 Invoke");
            var ps = invoke.GetParameters();
            Assert.AreEqual(1, ps.Length, "focusChanged 是 Action<bool>，应当只有 1 个参数（实测 Unity 2022.3）");
            Assert.AreEqual(typeof(bool), ps[0].ParameterType,
                "focusChanged 的参数应为 bool（是否获得焦点）");

            // 源码侧：处理器必须恰好接一个 bool 参数，否则编译不过
            string code = StripComments(ReadOwnSource("LubanAutoTrigger.cs"));
            bool signatureOk = Regex_Contains(code,
                @"static\s+void\s+OnFocusChanged\s*\(\s*bool\s+\w+\s*\)");
            Assert.IsTrue(signatureOk,
                "🔴 LubanAutoTrigger.OnFocusChanged 必须写成 OnFocusChanged(bool focused)，"
                + "以匹配 Action<bool> 的 focusChanged");
        }

        [Test]
        public void E3_焦点自动导表_默认必须是关的()
        {
            // 🔴 这是人的决策，不是技术限制：focusChanged 只知道「Unity 拿到焦点了」，
            //    不知道你有没有改表，所以每次切回来都会全量重导一遍 ——
            //    改表改到一半切回来会被校验炸一片红，什么都没改也照跑一遍。
            //    信任成本高于省下的那一次点击，所以默认关、手动导表。
            //
            // 断言源码而不是断言 LubanAutoTrigger.Enabled 的运行时值：
            // 后者会被本机 EditorPrefs 里已存的开关污染，测不出「默认」。
            string code = StripComments(ReadOwnSource("LubanAutoTrigger.cs"));

            bool defaultsOff = Regex_Contains(code,
                @"EditorPrefs\.GetBool\(\s*EnabledKey\s*,\s*false\s*\)");
            Assert.IsTrue(defaultsOff,
                "🔴 LubanAutoTrigger.Enabled 的默认值必须是 false（默认关，手动导表）");

            bool hasV2Key = code.Contains("Enabled.v2");
            Assert.IsTrue(hasV2Key,
                "🔴 EditorPrefs 的 key 必须换新名（带 .v2）：同一个 key 只改默认值，"
                + "对「EditorPrefs 里已经存了 true」的机器无效，那些人不换 key 拿不到新默认");
        }

        // ================================================================
        // F · 输出目录约定（§5.2-F）
        // ================================================================

        [Test]
        public void F1_生成物专用目录_注释里写明手写文件不得放入()
        {
            string src = ReadOwnSource("LubanProject.cs");
            Assert.IsTrue(src.Contains("生成物专用"), "🔴 注释里必须写明「生成物专用」");
            Assert.IsTrue(src.Contains("手写文件都不得放入") || src.Contains("手写文件不得放入"),
                "🔴 注释里必须写明手写文件不得放入");
            Assert.IsTrue(src.Contains("Scripts/Config/Gen/"),
                "🔴 必须写清将来的切换规则（改为输出 Gen/，手写物放上一层）");
        }

        [Test]
        public void F2_输出目录落在设计位置()
        {
            Assert.AreEqual(Path.Combine(Application.dataPath, "Scripts", "Config"),
                LubanProject.OutputCodeDir);
            Assert.AreEqual(Path.Combine(Application.dataPath, "StreamingAssets", "Luban"),
                LubanProject.OutputDataDir);
        }

        [Test]
        public void F3_暂存区在workspace内_与工程目录分离()
        {
            // 🔴 这是「校验失败绝不污染工程」的实现基础：
            // Luban 即使校验失败也会写盘，所以它的落盘位置不能是 Assets。
            Assert.IsTrue(LubanProject.StageCodeDir.StartsWith(LubanProject.Workspace),
                "暂存代码目录必须在 ConfigWorkspace 内");
            Assert.IsTrue(LubanProject.StageDataDir.StartsWith(LubanProject.Workspace),
                "暂存数据目录必须在 ConfigWorkspace 内");
            Assert.IsFalse(LubanProject.StageCodeDir.StartsWith(Application.dataPath),
                "🔴 暂存目录不能在 Assets 内");
        }

        // ================================================================
        // 引号规则（§2.5）· 含工程路径带空格的边界
        // ================================================================

        [Test]
        public void Q1_Windows引号_含空格要包裹()
        {
            Assert.AreEqual("\"T:\\My Games\\Jam\\Assets\"",
                Quote.QuoteOneForWindows(@"T:\My Games\Jam\Assets"));
            Assert.AreEqual(@"T:\GAMES\deepseaoil\Assets",
                Quote.QuoteOneForWindows(@"T:\GAMES\deepseaoil\Assets"));
        }

        [Test]
        public void Q2_Windows引号_结尾反斜杠翻倍()
        {
            // 🔴 规则：需要包裹时，结尾连续反斜杠数量翻倍（C:\x\ → "C:\x\\"），
            //    否则收尾引号会被反斜杠吃掉。
            //
            // 只对「需要包裹」的参数成立：无空格无引号的参数不包裹，结尾反斜杠也就无需翻倍
            // （实测确认，这是对的——没引号可吃）。所以断言走字符级结构比较，
            // 不用肉眼数反斜杠：字面量里数反斜杠是出过错的。
            Assert.AreEqual("\"T:\\My Games\\\\\"", Quote.QuoteOneForWindows(@"T:\My Games\"));

            // 含空格 + 结尾单个反斜杠 → 包裹且翻倍
            AssertTrailingBackslashes(Quote.QuoteOneForWindows(@"C:\x y\"), 2);
            // 含空格 + 结尾两个反斜杠 → 包裹且翻倍成 4
            AssertTrailingBackslashes(Quote.QuoteOneForWindows("C:\\x y\\\\"), 4);
            // 含空格 + 结尾无反斜杠 → 不翻倍
            AssertTrailingBackslashes(Quote.QuoteOneForWindows(@"C:\x y"), 0);
            // 无空格无引号 → 原样返回，不包裹
            Assert.AreEqual(@"C:\x\", Quote.QuoteOneForWindows(@"C:\x\"));
        }

        [Test]
        public void Q3_Windows引号_内部引号转义_空串()
        {
            Assert.AreEqual("\"a\\\"b\"", Quote.QuoteOneForWindows("a\"b"));
            Assert.AreEqual("\"\"", Quote.QuoteOneForWindows(""));
            // 不需要包裹的两种情形：纯反斜杠、无空格无引号
            Assert.AreEqual("\\", Quote.QuoteOneForWindows("\\"));
            Assert.AreEqual("C:\\x\\\\", Quote.QuoteOneForWindows("C:\\x\\\\"));
            // 空格 → 包裹
            Assert.AreEqual("\"a b\"", Quote.QuoteOneForWindows("a b"));
        }

        [Test]
        public void Q4_Posix引号_单引号转义()
        {
            Assert.AreEqual("'a'\\''b'", Quote.QuoteOneForPosix("a'b"));
            Assert.AreEqual("'/opt/My Games/x'", Quote.QuoteOneForPosix("/opt/My Games/x"));
        }

        [Test]
        public void Q5_整串组装_参数间空格分隔()
        {
            string s = Quote.QuoteForWindows(new[] { "dotnet", "/p/a b/Luban.dll", "--strict" });
            Assert.AreEqual("dotnet \"/p/a b/Luban.dll\" --strict", s);
        }

        // ================================================================
        // 无硬编码盘符
        // ================================================================

        [Test]
        public void H1_内核源码_无硬编码盘符()
        {
            // 只看「字符串字面量」，不看注释——注释里的示例路径（如 C:\x\）是文档，不是硬编码。
            // 断言用的模式本身也不写死在源码里，否则这个断言会自己命中自己。
            string driveLetter = "C";
            string drivePattern = driveLetter + ":";

            foreach (var file in new[] { "LubanProject.cs", "Quote.cs", "LubanImport.cs", "LubanAutoTrigger.cs" })
            {
                string literals = SumUpStringLiterals(ReadOwnSource(file));
                int at = literals.IndexOf(drivePattern, System.StringComparison.OrdinalIgnoreCase);
                Assert.IsTrue(at < 0,
                    "🔴 " + file + " 的字符串字面量里出现硬编码盘符："
                    + literals.Substring(Math.Max(0, at - 20), Math.Min(60, literals.Length - Math.Max(0, at - 20))));
            }
        }

        // ================================================================
        // 输出解析（Luban 的 [new]/[overwrite] 行）
        // ================================================================

        [Test]
        public void P1_写出文件解析()
        {
            const string sample =
                "2026/09/30 01:36:39.000|INFO|process data target:\"json\" begin\n" +
                "2026/09/30 01:36:39.001|INFO|[new] T:\\x\\output\\data/demo_tbweapon.json \n" +
                "2026/09/30 01:36:39.002|INFO|[overwrite] T:\\x\\output\\data/demo_tbitem.json \n" +
                "2026/09/30 01:36:39.003|INFO|[new] T:\\x\\output\\code/demo/Weapon.cs \n" +
                "2026/09/30 01:36:39.004|INFO|[remove] T:\\x\\output\\code/old.cs\n" +
                "2026/09/30 01:36:39.005|INFO|bye~\n";

            var files = LubanImport.ParseWrittenFiles(sample);
            // ParseWrittenFiles 内部按 OrdinalIgnoreCase 排序（大写字母在前）
            CollectionAssert.AreEqual(
                new List<string> { "demo_tbitem.json", "demo_tbweapon.json", "Weapon.cs" },
                files);
        }

        // ================================================================
        // 工具
        // ================================================================

        /// <summary>
        /// 去掉注释，只留代码。
        /// 🔴 需要它是因为内核文件的注释里**故意**写了「不要用 XXX」「C:\x\ → "C:\x\\"」
        /// 这类反例文本，直接对整份源码做关键子串断言会命中注释而误报。
        /// </summary>
        public static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                // 块注释
                if (c == '/' && n == '*')
                {
                    int end = src.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    i = end < 0 ? src.Length : end + 1;
                    sb.Append(' ');
                    continue;
                }
                // 行注释
                if (c == '/' && n == '/')
                {
                    int end = src.IndexOf('\n', i + 2);
                    i = end < 0 ? src.Length - 1 : end;
                    sb.Append('\n');
                    continue;
                }
                // 普通字符串（保留，因为「字面量」本身是断言对象）
                if (c == '"')
                {
                    sb.Append(c);
                    i++;
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) { sb.Append(src[i]); i++; }
                        sb.Append(src[i]);
                        i++;
                    }
                    if (i < src.Length) sb.Append(src[i]);
                    continue;
                }
                // 逐字字符串 @"..."
                if (c == '@' && n == '"')
                {
                    sb.Append(c).Append(n);
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { sb.Append("\"\""); i += 2; continue; }
                            sb.Append('"');
                            break;
                        }
                        sb.Append(src[i]);
                        i++;
                    }
                    continue;
                }
                // 字符字面量 'x' / '\n' / '\''
                if (c == '\'')
                {
                    sb.Append(c);
                    if (i + 1 < src.Length && src[i + 1] == '\\' && i + 2 < src.Length) { sb.Append(src[i + 1]); sb.Append(src[i + 2]); i += 2; }
                    else if (i + 1 < src.Length) { sb.Append(src[i + 1]); i++; }
                    if (i + 1 < src.Length && src[i + 1] == '\'') { sb.Append('\''); i++; }
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 只取「字符串字面量」拼成一串，供「无硬编码盘符」这类断言使用。
        /// 与 StripComments 反向：那里要留代码去注释，这里要留字面量去注释。
        /// </summary>
        public static string SumUpStringLiterals(string src)
        {
            string code = StripComments(src);
            var sb = new StringBuilder();
            for (int i = 0; i < code.Length; i++)
            {
                bool verbatim = code[i] == '@' && i + 1 < code.Length && code[i + 1] == '"';
                if (code[i] != '"' && !verbatim) continue;

                int start = i;
                i += verbatim ? 2 : 1;
                while (i < code.Length && code[i] != '"')
                {
                    if (!verbatim && code[i] == '\\' && i + 1 < code.Length) i++;
                    i++;
                }
                sb.Append(code, start, Math.Min(i + 1, code.Length) - start).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 断言「被引号包裹的字符串，结尾恰好有 n 个反斜杠」。
        /// 用字符级结构比较代替肉眼数字面量里的反斜杠——后者在本次落地时出过错。
        /// </summary>
        static void AssertTrailingBackslashes(string quoted, int expected)
        {
            Assert.IsTrue(quoted.Length >= 2 && quoted[0] == '"' && quoted[quoted.Length - 1] == '"',
                "参数未被双引号包裹：[" + quoted + "]");

            int n = 0;
            for (int i = quoted.Length - 2; i >= 1 && quoted[i] == '\\'; i--) n++;

            Assert.AreEqual(expected, n,
                "结尾反斜杠数量不对（含引号原样：[" + quoted + "]）");
        }

        static int CountOf(string[] arr, string value)
        {
            int n = 0;
            foreach (var a in arr) if (a == value) n++;
            return n;
        }

        static bool Regex_Contains(string input, string pattern)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(input, pattern);
        }

        /// <summary>
        /// 读内核源码做静态断言。🔴 断言源码文本是刻意的：
        /// §5.2 的 B/C/D/E/F 五项本质是「代码里必须/不得出现某种写法」，
        /// 只有源码级断言才能在每次 Test Runner 运行时替人守住它们。
        /// </summary>
        static string ReadOwnSource(string fileName)
        {
            string path = FindRepoFile(fileName);
            return path == null ? string.Empty : File.ReadAllText(path, Encoding.UTF8);
        }

        static string FindRepoFile(string fileName)
        {
            string editorDir = Path.Combine(LubanProject.DataPath, "Editor");
            if (Directory.Exists(editorDir))
            {
                var hits = Directory.GetFiles(editorDir, fileName, SearchOption.AllDirectories);
                if (hits.Length > 0) return hits[0];
            }
            return null;
        }
    }
}
