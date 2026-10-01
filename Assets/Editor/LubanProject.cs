// ---------------------------------------------------------------------------
// 配表工作流 · 布局与命令行（内核，不碰 Unity 编辑器 API 之外的任何东西）
//
// 【硬约定】以下两个目录是「生成物专用目录」：
//     Assets/Scripts/Generated/Config/   ← Luban 生成的 C#
//     Assets/StreamingAssets/Luban/      ← Luban 生成的 JSON
//   导表时整个目录会被镜像覆盖（多余文件被删除）。
//   任何手写文件都不得放入，放了下次导表必丢。
//   （第三处生成物是 Assets/Scripts/Generated/Input/InputSys.cs，由 .inputactions 生成。）
//
// 所有路径由 Application.dataPath 推算，无硬编码盘符：挪工程不改配置。
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.EditorTools
{
    /// <summary>一次子进程调用的完整描述（FileName + 已转义的 Arguments）。</summary>
    public struct ProcessCommand
    {
        public string FileName;
        public string Arguments;
        public string WorkingDirectory;
        /// <summary>未转义的参数表，仅用于「路径自检」打印与测试断言。</summary>
        public string[] Argv;
    }

    /// <summary>Luban 工具本体 / 配置文件 / 输出目录的存在性检查结果。</summary>
    public struct PrecheckResult
    {
        public bool Ok;
        public string Message;
    }

    public static class LubanProject
    {
        /// <summary>中间产物目录名（相对 workspace），gitignore。</summary>
        const string StageCodeRel = "output/code";
        const string StageDataRel = "output/data";
        /// <summary>日志目录名（相对 workspace），gitignore。</summary>
        public const string LogsRel = "logs";

        public static string DataPath { get { return Application.dataPath; } }
        public static string ProjectRoot { get { return Path.GetDirectoryName(Application.dataPath); } }
        public static string Workspace { get { return Path.Combine(ProjectRoot, "ConfigWorkspace"); } }
        public static string LubanDll { get { return Path.Combine(Workspace, "Tools", "Luban", "Luban.dll"); } }
        public static string LubanConf { get { return Path.Combine(Workspace, "luban.conf"); } }
        public static string LogsDir { get { return Path.Combine(Workspace, LogsRel); } }

        /// <summary>Luban 的落盘位置：暂存区。绝不能直接指向 Assets（见 LubanImport 顶部说明）。</summary>
        public static string StageCodeDir { get { return Path.Combine(Workspace, "output", "code"); } }
        public static string StageDataDir { get { return Path.Combine(Workspace, "output", "data"); } }

        /// <summary>生成物专用目录（成功后由暂存区镜像覆盖）。</summary>
        public static string OutputCodeDir { get { return Path.Combine(DataPath, "Scripts", "Generated", "Config"); } }
        public static string OutputDataDir { get { return Path.Combine(DataPath, "StreamingAssets", "Luban"); } }

        /// <summary>pathValidator.rootDir：表里 #path=unity 的路径基准 = Assets。</summary>
        public static string PathValidatorRoot { get { return DataPath; } }

        /// <summary>Excel 源表所在目录（也是 ~$ 锁文件的出现位置）。</summary>
        public static string DataDir { get { return Path.Combine(Workspace, "Data"); } }

        // ------------------------------------------------------------------
        // 预检：启动进程之前
        // ------------------------------------------------------------------

        public static PrecheckResult Precheck()
        {
            if (!Directory.Exists(Workspace))
                return Fail("缺配置目录：{0}", Workspace);
            if (!File.Exists(LubanDll))
                return Fail("缺工具文件：{0}", LubanDll);
            if (!File.Exists(LubanConf))
                return Fail("缺配置：{0}", LubanConf);

            // 🔴 Excel 锁文件检测。实测：Excel 开着 xlsx 时 Luban **仍能跑成功**，
            //    读的是磁盘上已保存的那一版 —— 所以真正的风险不是读表失败，
            //    而是「改了没保存就导入，静默用旧数据」。这里提前拦下，给出可操作的提示。
            var locks = FindExcelLockFiles();
            if (locks.Count > 0)
                return Fail("有表格正被 Excel 占用，导入会失败。\n"
                    + "请先在 Excel 里【保存】并【关闭】下列表格，然后重试：\n"
                    + "  " + string.Join("\n  ", TableNamesOf(locks)));

            return new PrecheckResult { Ok = true, Message = null };
        }

        /// <summary>
        /// 列出 Data 目录下的 Excel 锁文件（Excel 打开工作簿时会生成 `~$&lt;原文件名&gt;`）。
        /// 🔴 只检测、只报告，**绝不自动删除**——那是用户数据（Excel 的占用标记），
        /// 脚本不应删除它不理解的文件（草案附录第 3 条）。
        /// </summary>
        public static List<string> FindExcelLockFiles()
        {
            var list = new List<string>();
            if (!Directory.Exists(DataDir))
                return list;

            try
            {
                foreach (var f in Directory.GetFiles(DataDir, "~$*"))
                    list.Add(f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Luban] 检查 Excel 锁文件时出错：" + ex.Message);
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>`~$weapon.xlsx` → `weapon.xlsx`，去掉 Excel 的占用标记前缀。</summary>
        public static List<string> TableNamesOf(List<string> lockFiles)
        {
            var names = new List<string>();
            foreach (var p in lockFiles)
            {
                string n = Path.GetFileName(p);
                if (n.StartsWith("~$", StringComparison.Ordinal))
                    n = n.Substring(2);
                names.Add(n);
            }
            return names;
        }

        static PrecheckResult Fail(string fmt, params object[] args)
        {
            return new PrecheckResult { Ok = false, Message = string.Format(fmt, args) };
        }

        // ------------------------------------------------------------------
        // 命令行（🔴 逐项都有理由，不要增删）
        //
        //   --conf   配置文件
        //   -t       target = client
        //   --strict 缺它则校验失败仍返回退出码 0 → 整套校验静默失效
        //   -c/-d    代码目标 cs-simple-json、数据目标 json
        //   -x       outputCodeDir / outputDataDir 指向暂存区
        //   -x       pathValidator.rootDir（不写进 conf，由命令行注入）
        // ------------------------------------------------------------------

        /// <summary>导出：走暂存区，成功后由 LubanImport 镜像拷贝进工程。</summary>
        public static ProcessCommand BuildExportCommand()
        {
            var argv = new List<string>
            {
                LubanDll,
                "--conf", LubanConf,
                "-t", "client",
                "--strict",
                "-c", "cs-simple-json",
                "-d", "json",
                "-x", "outputCodeDir=" + StageCodeDir,
                "-x", "outputDataDir=" + StageDataDir,
                "-x", "pathValidator.rootDir=" + PathValidatorRoot,
            };
            return Build(argv);
        }

        /// <summary>
        /// 仅校验：-f（不产出）+ outputSaver=null（双保险，实测零写入）+ --strict（失败算失败）。
        /// 注意 -f 与 --strict 是两件独立的事，两者都要。
        /// </summary>
        public static ProcessCommand BuildValidateCommand()
        {
            var argv = new List<string>
            {
                LubanDll,
                "--conf", LubanConf,
                "-t", "client",
                "--strict",
                "-f",
                "-x", "outputSaver=null",
                "-x", "pathValidator.rootDir=" + PathValidatorRoot,
            };
            return Build(argv);
        }

        static ProcessCommand Build(List<string> argv)
        {
            string fileName;
            string arguments;

#if UNITY_EDITOR_WIN
            fileName = "dotnet";
            // 手工拼引号。🔴 不要用 ProcessStartInfo.ArgumentList：
            // Unity 2022.3 的 API 兼容级别是 .NET Standard 2.1，该属性是否可用未核实，
            // 若不可用会直接 CS1061 编译失败。
            arguments = Quote.QuoteForWindows(argv);
#else
            // macOS：GUI 进程 PATH 里通常找不到 dotnet，必须经登录 shell。
            fileName = "/bin/bash";
            string inner = "dotnet " + Quote.QuoteForPosix(argv);
            arguments = "-cl " + Quote.QuoteForPosix(new[] { inner });
#endif
            return new ProcessCommand
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Workspace,
                Argv = argv.ToArray(),
            };
        }

        /// <summary>「路径自检」用：把全部推算路径摊平成人可读清单。</summary>
        public static IEnumerable<string> DescribePaths()
        {
            yield return "DataPath          = " + DataPath;
            yield return "ProjectRoot       = " + ProjectRoot;
            yield return "Workspace         = " + Workspace + Exist(Directory.Exists(Workspace));
            yield return "LubanDll          = " + LubanDll + Exist(File.Exists(LubanDll));
            yield return "LubanConf         = " + LubanConf + Exist(File.Exists(LubanConf));
            yield return "StageCodeDir      = " + StageCodeDir;
            yield return "StageDataDir      = " + StageDataDir;
            yield return "OutputCodeDir     = " + OutputCodeDir + "   ← 生成物专用，手写文件不得放入";
            yield return "OutputDataDir     = " + OutputDataDir + "   ← 生成物专用，手写文件不得放入";
            yield return "PathValidatorRoot = " + PathValidatorRoot + Exist(Directory.Exists(PathValidatorRoot));
            yield return "LogsDir           = " + LogsDir;
            yield return "DataDir           = " + DataDir + Exist(Directory.Exists(DataDir));

            var locks = FindExcelLockFiles();
            yield return "Excel 锁文件      = " + (locks.Count == 0
                ? "无（表未被 Excel 占用）"
                : locks.Count + " 个：" + string.Join(", ", TableNamesOf(locks).ToArray()));
            yield return "FileName          = " + LaunchFileName();
        }

        static string LaunchFileName()
        {
#if UNITY_EDITOR_WIN
            return "dotnet";
#else
            return "/bin/bash";
#endif
        }

        static string Exist(bool ok)
        {
            return ok ? "   [存在]" : "   [缺失!]";
        }
    }
}
