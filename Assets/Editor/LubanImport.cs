// ---------------------------------------------------------------------------
// 配表工作流 · 导表内核
//
// 🔴 为什么走「暂存区 + 成功后镜像拷贝」，而不是把 -x outputCodeDir 直接指向 Assets：
//   实测（Luban 5.1.0，本次落地时亲自跑出）——
//       INFO|process code target:"cs-simple-json" begin
//       INFO|validation begin
//       ERROR|… 找不到对应文件
//       INFO|validation end
//       INFO|[new] …/code/Tables.cs          ← 校验失败之后，文件照样写
//       ERROR|存在校验失败。退出码: 1
//   --strict 只决定【退出码】，不阻止写盘；写入发生在校验之后。
//   而且 Luban 每次都先 [remove] 整个输出目录再 [new]。
//   所以若直接写进 Assets：一次失败的导表会当场删掉并重写整个生成目录，
//   Unity 文件监视会立刻导入坏数据——即使我们不调 AssetDatabase.Refresh() 也挡不住。
//   暂存区方案让「校验失败绝不污染工程」真正成立，顺带避免半个目录的中间态。
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;   // 与 System.Diagnostics.Debug 同名，必须消歧

namespace DeepseaOil.EditorTools
{
    public class LubanRunResult
    {
        public bool Success;
        public bool Launched;
        public int ExitCode;
        public string Output = string.Empty;
        public string ErrorMessage;
        public string LogPath;
        /// <summary>本次 Luban 真正写出的文件（相对暂存目录，形如 dso/Enemy.cs）。</summary>
        public List<string> WrittenFiles = new List<string>();
        /// <summary>镜像拷贝时从工程目录删掉的孤儿文件（绝对路径）。</summary>
        public List<string> DeletedFiles = new List<string>();
    }

    public static class LubanImport
    {
        /// <summary>上次导表未完成则跳过新触发。</summary>
        public static bool IsRunning { get; private set; }

        /// <summary>等异步读收尾的上限。进程已退出，这只影响最多能拿到多少尾部输出。</summary>
        const int AsyncDrainTimeoutMs = 2000;

        /// <summary>Console 里逐行回显的上限（超出部分仍完整落盘到日志文件）。</summary>
        const int ConsoleTailLines = 200;

        const string LogFilePrefix = "luban_";

        // ------------------------------------------------------------------
        // 入口
        // ------------------------------------------------------------------

        /// <summary>
        /// 导表（菜单 / 焦点触发都走这里）。内部自带预检与全部日志输出。
        /// 返回值仅用于调用方决定是否刷新节流计时。
        /// </summary>
        public static LubanRunResult RunExport()
        {
            if (IsRunning)
                return NotLaunched(null);

            // 🔴 预检在启动进程之前
            var pre = LubanProject.Precheck();
            if (!pre.Ok)
            {
                // 不弹窗：后台自动触发时弹窗会堆积
                Debug.LogError("[Luban] " + pre.Message + "（未启动进程）");
                return NotLaunched(pre.Message);
            }

            IsRunning = true;
            AssetDatabase.DisallowAutoRefresh();   // 导表期间不让 Unity 抢 I/O 与触发导入
            try
            {
                return Execute(LubanProject.BuildExportCommand(), true);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                IsRunning = false;
            }
        }

        /// <summary>
        /// 仅校验：-f + outputSaver=null（实测零写入），成功/失败都只报日志，绝不碰工程目录。
        /// </summary>
        public static LubanRunResult RunValidate()
        {
            if (IsRunning)
                return NotLaunched(null);

            var pre = LubanProject.Precheck();
            if (!pre.Ok)
            {
                Debug.LogError("[Luban] " + pre.Message + "（未启动进程）");
                return NotLaunched(pre.Message);
            }

            IsRunning = true;
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                return Execute(LubanProject.BuildValidateCommand(), false);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                IsRunning = false;
            }
        }

        /// <summary>给焦点触发器用：只看预检，不启动进程、不写任何日志。</summary>
        public static bool Precheck()
        {
            return LubanProject.Precheck().Ok;
        }

        static LubanRunResult NotLaunched(string message)
        {
            return new LubanRunResult { Success = false, Launched = false, ExitCode = -1, ErrorMessage = message };
        }

        // ------------------------------------------------------------------
        // 进程调用（🔴 防死锁 + 防乱码）
        // ------------------------------------------------------------------

        static LubanRunResult Execute(ProcessCommand cmd, bool stageAndPublish)
        {
            var sb = new StringBuilder();
            int exitCode;
            var psi = new ProcessStartInfo
            {
                FileName = cmd.FileName,
                Arguments = cmd.Arguments,
                WorkingDirectory = cmd.WorkingDirectory,   // 🔴 必须显式设置：conf 里全是相对路径
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // 🔴 实测：Luban 经管道输出的字节是 UTF-8。不显式指定的话，
                // 中文 Windows 上 .NET 会按 GBK 解码 → Console 里中文报错全成乱码。
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };

            using (var proc = new Process { StartInfo = psi })
            {
                // 🔴 只做异步读。重定向 + 同步 ReadToEnd() 在输出量大时会写满缓冲区：
                // 子进程阻塞等待写、父进程阻塞等待退出 → Unity 死锁。
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };

                try
                {
                    proc.Start();
                }
                catch (Exception ex)
                {
                    return NotLaunched(DescribeLaunchFailure(cmd.FileName, ex));
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                // await 退出（第一次），再等异步读收尾（第二次，带上限）
                proc.WaitForExit();
                proc.WaitForExit(AsyncDrainTimeoutMs);
                exitCode = proc.ExitCode;
            }

            var result = new LubanRunResult
            {
                Launched = true,
                ExitCode = exitCode,
                Success = exitCode == 0,
                Output = sb.ToString(),
            };

            result.WrittenFiles = ParseWrittenFiles(result.Output);
            result.LogPath = WriteLogFile(cmd, result);

            if (result.Success && stageAndPublish)
            {
                // 步骤 1：暂存区必须真的有代码产出，否则是工具装错/模板缺失
                string tablesCs = Path.Combine(LubanProject.StageCodeDir, "Tables.cs");
                if (!File.Exists(tablesCs))
                {
                    result.Success = false;
                    result.ErrorMessage = "退出码 0 但暂存区没有 Tables.cs：" + tablesCs + "（工具目录可能不完整）";
                    Report(new LubanRunResult { Success = false, ExitCode = exitCode, ErrorMessage = result.ErrorMessage });
                    return result;
                }

                // 步骤 2：镜像拷贝（只有到这里才第一次碰工程目录）
                MirrorCopy(LubanProject.StageCodeDir, LubanProject.OutputCodeDir, result.DeletedFiles);
                MirrorCopy(LubanProject.StageDataDir, LubanProject.OutputDataDir, result.DeletedFiles);

                // 步骤 3：刷新（触发代码与 JSON 重新导入，进而触发脚本编译）
                AssetDatabase.Refresh();
            }

            Report(result);
            return result;
        }

        static string DescribeLaunchFailure(string fileName, Exception ex)
        {
            string hint;
#if UNITY_EDITOR_WIN
            hint = "请安装 .NET SDK 8+ 后重启 Unity";
#else
            hint = "请确认 dotnet 在登录 shell PATH 中";
#endif
            return string.Format("无法启动 \"{0}\"（{1}）：{2} —— {3}",
                fileName, ex.GetType().Name, ex.Message, hint);
        }

        // ------------------------------------------------------------------
        // 输出解析
        // ------------------------------------------------------------------

        static readonly Regex[] WrittenFilePatterns =
        {
            new Regex(@"\|INFO\|\s*\[(?:new|overwrite)\]\s+(?<path>[^\r\n]+?)\s*$", RegexOptions.Multiline),
        };

        /// <summary>从 Luban 日志里刮出本次写出的文件（形如 dso/Enemy.cs）。</summary>
        public static List<string> ParseWrittenFiles(string output)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(output)) return list;
            foreach (var re in WrittenFilePatterns)
            {
                foreach (Match m in re.Matches(output))
                {
                    string p = m.Groups["path"].Value.Trim().Replace('\\', '/');
                    int slash = p.LastIndexOf('/');
                    string rel = slash >= 0 ? p.Substring(slash + 1) : p;
                    if (rel.Length == 0) continue;
                    if (!list.Contains(rel)) list.Add(rel);
                }
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        // ------------------------------------------------------------------
        // 报告
        // ------------------------------------------------------------------

        static void Report(LubanRunResult r)
        {
            if (!r.Launched)
            {
                if (!string.IsNullOrEmpty(r.ErrorMessage))
                    Debug.LogError("[Luban] " + r.ErrorMessage);
                return;
            }

            var lines = SplitLines(r.Output);
            var errorLines = lines.Where(IsErrorLine).ToList();

            if (r.Success)
            {
                // 措辞刻意区分「没干活」与「已是最新」：Luban 按需写盘，
                // 而且每次都跑完整流程（读全部表 + 校验 + 生成），只是内容没变就不重写。
                Debug.Log(r.WrittenFiles.Count > 0
                    ? string.Format("[Luban] 表格数据导入成功（退出码 0）。本次写出 {0} 个文件{1}",
                        r.WrittenFiles.Count,
                        r.DeletedFiles.Count > 0 ? "，删除孤儿文件 " + r.DeletedFiles.Count + " 个" : "")
                    : "[Luban] 表格数据导入成功（退出码 0）。表已是生成物的最新状态，本次无需重写任何文件");

                if (r.WrittenFiles.Count > 0)
                    Debug.Log("[Luban] 文件清单：\n  " + string.Join("\n  ", r.WrittenFiles));
                foreach (var d in r.DeletedFiles)
                    Debug.Log("[Luban] 删除：" + d);
                LogTail(lines, ConsoleTailLines);
                Debug.Log("[Luban] 完整日志：" + r.LogPath);
                return;
            }

            // 失败：中文报错本身已含表名 / 主键 / 字段 / 值，交给策划可直接定位
            Debug.LogError(string.Format("[Luban] 表格数据导入失败（退出码 {0}）{1}{2}",
                r.ExitCode,
                string.IsNullOrEmpty(r.ErrorMessage) ? "" : "：" + r.ErrorMessage,
                " 工程目录未被修改。"));

            if (errorLines.Count > 0)
                Debug.LogError("[Luban] 错误行（" + errorLines.Count + " 条）：\n" + string.Join("\n", errorLines));

            // 纯失败时错误行可能不够定位，再补一段完整尾部
            if (r.WrittenFiles.Count == 0)
                LogTail(lines, ConsoleTailLines);

            if (!string.IsNullOrEmpty(r.LogPath))
                Debug.LogError("[Luban] 完整日志：" + r.LogPath);
        }

        static void LogTail(List<string> lines, int max)
        {
            if (lines.Count == 0) return;
            int skip = Math.Max(0, lines.Count - max);
            var tail = lines.Skip(skip).ToList();
            var text = string.Join("\n", tail);
            if (skip > 0) text = "…（前 " + skip + " 行略，完整见日志文件）\n" + text;
            Debug.Log("[Luban] 输出：\n" + text);
        }

        static bool IsErrorLine(string line)
        {
            return line.IndexOf("|ERROR|", StringComparison.Ordinal) >= 0
                || line.IndexOf("存在校验失败", StringComparison.Ordinal) >= 0
                || line.IndexOf("Unhandled exception", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static List<string> SplitLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return new List<string>();
            return s.Replace("\r\n", "\n").Replace('\r', '\n')
                    .Split('\n')
                    .Where(l => l.Length > 0)
                    .ToList();
        }

        static string WriteLogFile(ProcessCommand cmd, LubanRunResult r)
        {
            try
            {
                Directory.CreateDirectory(LubanProject.LogsDir);
                string path = Path.Combine(LubanProject.LogsDir,
                    LogFilePrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                var sb = new StringBuilder();
                sb.AppendLine("# " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("# FileName  = " + cmd.FileName);
                sb.AppendLine("# Arguments = " + cmd.Arguments);
                sb.AppendLine("# WorkDir   = " + cmd.WorkingDirectory);
                sb.AppendLine("# ExitCode  = " + r.ExitCode);
                sb.AppendLine(new string('-', 72));
                sb.Append(r.Output);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Luban] 写日志文件失败：" + ex.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------
        // 镜像拷贝
        // ------------------------------------------------------------------

        /// <summary>
        /// 把 src 目录完整镜像到 dst：先删 dst 里 src 没有的文件，再拷贝新增/内容变化的文件。
        /// 🔴 反向删除必须有：否则删掉的表会在 StreamingAssets 留下孤儿 JSON，
        /// 下次编译又拿不到对应的 C# 类型，很难查。
        /// 例外：dst 里的 .meta 永不删除——它由 Unity 生成，清目录时自会一起回收。
        /// </summary>
        public static void MirrorCopy(string srcDir, string dstDir, List<string> deletedLog)
        {
            if (!Directory.Exists(srcDir))
                return;

            Directory.CreateDirectory(dstDir);

            var srcFiles = new HashSet<string>(
                Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories)
                    .Select(p => Rel(srcDir, p)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dstPath in Directory.GetFiles(dstDir, "*", SearchOption.AllDirectories))
            {
                string rel = Rel(dstDir, dstPath);
                if (srcFiles.Contains(rel)) continue;
                if (rel.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                if (deletedLog != null) deletedLog.Add(dstPath);
                try { File.Delete(dstPath); }
                catch (Exception ex) { Debug.LogWarning("[Luban] 删除失败：" + dstPath + " —— " + ex.Message); }
            }

            foreach (var srcPath in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                string rel = Rel(srcDir, srcPath);
                string dstPath = Path.Combine(dstDir, rel);
                string dir = Path.GetDirectoryName(dstPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(dstPath) && SameContent(srcPath, dstPath))
                    continue;   // 内容一致就不动它：生成的 .meta 与 GUID 保持稳定，git diff 干净

                File.Copy(srcPath, dstPath, true);
            }

            RemoveEmptyDirectories(dstDir);
        }

        static string Rel(string root, string full)
        {
            string r = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? root
                : root + Path.DirectorySeparatorChar;
            return full.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? full.Substring(r.Length) : full;
        }

        static bool SameContent(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            return Hash(a) == Hash(b);
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(fs));
            }
        }

        static void RemoveEmptyDirectories(string root)
        {
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch { /* 删不掉就留着，无害 */ }
            }
        }
    }
}
