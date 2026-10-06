// ---------------------------------------------------------------------------
// 收口一致性 · 行为测试
//
// 守的是"收口"这件事本身：如果哪天有人又写了一个 .Instance、又加了一个自驱 Update，
// 编译不会报错、运行也看不出来 —— 只有把这两条口径变成机器可判的断言，它才会红。
//
// 三条断言：
//   1. 全仓 .Instance 只出现在白名单里（GameRoot / Singleton / MonoMgr）
//   2. Assets/Scripts 下 void Update / FixedUpdate / LateUpdate 只出现在白名单里
//      （GameRoot / MonoMgr）—— "每帧只有一个驱动发起者"
//   3. BaseManager 这个"反射自建单例"的基类已删除且零引用
//
// 扫描口径：读**源码文本**，逐行判断，注释行（// /// /* *）跳过 ——
// 注释里提旧写法是文档，不是调用点。
//
// 为什么放在 Assets/Tests/Tools（DeepseaOil.EditorTools.Tests）：
// 那个程序集看不见 Assembly-CSharp（故意的），而本测试只需要读文件，不需要任何游戏类型。
//
// 跑法：Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DeepseaOil.EditorTools.Tests
{
    public class 收口一致性Tests
    {
        /// <summary>允许出现在 <c>Xxx.Instance</c> 里的接收者。</summary>
        /// <remarks>
        /// 判据是<b>接收者</b>而不是"哪个文件"：调用点可以分布在任何地方
        /// （面板、场景根、输入采样器都经 <c>GameRoot.Instance</c> 取件），
        /// 被禁掉的是"再出现第二个单例类型"。
        /// </remarks>
        private static readonly HashSet<string> InstanceOwners = new()
        {
            "GameRoot",    // 唯一真单例
            "MonoMgr",     // 临时例外：协程宿主，用户限 UI 层内
        };

        /// <summary>允许出现 <c>Singleton&lt;T&gt;</c> 继承的文件名。</summary>
        private static readonly HashSet<string> SingletonWhitelist = new()
        {
            "Singleton.cs",    // 基类自己
            "GameRoot.cs",     // 唯一真单例
            "MonoMgr.cs",      // 临时例外
        };

        /// <summary>允许出现 Unity 逐帧回调的文件名。</summary>
        private static readonly HashSet<string> DriveWhitelist = new()
        {
            "GameRoot.cs",     // 唯一的驱动发起者（渲染帧 + 物理帧）
            "MonoMgr.cs",      // 临时例外：三条转发事件，用户限 UI 层内
        };

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string ScriptsDir => Path.Combine(Application.dataPath, "Scripts");

        /// <summary>扫描一行行源码；返回值里 <c>File</c> 是文件名（不含目录），<c>Line</c> 从 1 起。</summary>
        private static IEnumerable<(string File, int Line, string Text)> ScanCode()
        {
            Assert.IsTrue(Directory.Exists(ScriptsDir), "找不到源码目录：" + ScriptsDir);

            foreach (string path in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories))
            {
                string file = Path.GetFileName(path);
                int lineNo = 0;

                foreach (string raw in File.ReadAllLines(path))
                {
                    lineNo++;

                    string trimmed = raw.TrimStart();

                    // 注释行不算调用点（注释里提旧写法是文档）
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*")) continue;

                    yield return (file, lineNo, raw);
                }
            }
        }

        [Test]
        public void Instance出口只剩GameRoot与MonoMgr()
        {
            var offenders = new List<string>();

            foreach ((string file, int line, string text) in ScanCode())
            {
                // 抓 "接收者.Instance"：接收者必须是白名单里的类型
                foreach (Match m in Regex.Matches(text, @"([A-Za-z_][A-Za-z0-9_]*)\.Instance\b"))
                {
                    string owner = m.Groups[1].Value;

                    if (InstanceOwners.Contains(owner)) continue;

                    offenders.Add($"{file}:{line}  {text.Trim()}");
                    break;
                }
            }

            Assert.IsEmpty(offenders,
                "全仓的 Instance 出口只允许 GameRoot（＋ MonoMgr 这个临时例外）。"
                + "新出现的取用点请改成构造注入，或经 GameRoot 暴露的属性（UI / Game / Audio）：\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void 逐帧回调只出现在白名单里()
        {
            var offenders = new List<string>();

            foreach ((string file, int line, string text) in ScanCode())
            {
                if (!Regex.IsMatch(text, @"void\s+(Update|FixedUpdate|LateUpdate)\s*\(\s*\)")) continue;
                if (DriveWhitelist.Contains(file)) continue;

                offenders.Add($"{file}:{line}  {text.Trim()}");
            }

            Assert.IsEmpty(offenders,
                "每帧只有一个驱动发起者：Assets/Scripts 下除了 GameRoot（与 MonoMgr 这个临时例外），"
                + "任何件都不许自驱 Update / FixedUpdate / LateUpdate —— 场景级对象一律由 GameRoot 驱动。"
                + "要加驱动的件请实现 ISceneRoot ＋ IRenderTicked / IPhysicsTicked 并注册：\n  "
                + string.Join("\n  ", offenders));
        }

        [Test]
        public void 反射单例基类已退场()
        {
            Assert.IsFalse(File.Exists(Path.Combine(ScriptsDir, "Foundation", "BaseManager.cs")),
                "BaseManager.cs 应当已删除：反射自建单例把装配权下放给了任何调用点。");

            var offenders = new List<string>();

            foreach ((string file, int line, string text) in ScanCode())
            {
                if (!text.Contains("BaseManager")) continue;

                offenders.Add($"{file}:{line}  {text.Trim()}");
            }

            Assert.IsEmpty(offenders, "BaseManager 已退场，不该再被引用：\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void 单例泛型只服务GameRoot与MonoMgr()
        {
            var offenders = new List<string>();

            foreach ((string file, int line, string text) in ScanCode())
            {
                if (!text.Contains("Singleton<")) continue;
                if (SingletonWhitelist.Contains(file)) continue;

                offenders.Add($"{file}:{line}  {text.Trim()}");
            }

            Assert.IsEmpty(offenders,
                "Singleton<T> 只留给 GameRoot（＋ MonoMgr 这个临时例外）。"
                + "新的进程级件请做成普通类、由 GameRoot 构造并持有：\n  "
                + string.Join("\n  ", offenders));
        }
    }
}
