// ---------------------------------------------------------------------------
// 命令行引号规则（纯函数，可单测）
//
// 为什么不用 ProcessStartInfo.ArgumentList：Unity 2022.3 的 API 兼容级别是
// .NET Standard 2.1，该属性是否在 profile 内可用未核实；不可用会 CS1061 编译失败。
// 手工拼引号在所有版本上都成立，代价是两套平台规则要自己维护——就是本文件。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

namespace DeepseaOil.EditorTools
{
    public static class Quote
    {
        /// <summary>
        /// Windows：参数整串交给运行时，由它按 MSVCRT 规则拆分。
        /// 规则：含空格或为空 → 用 " 包裹；内部 " → \"；结尾连续反斜杠数量翻倍
        /// （否则收尾的 " 会被反斜杠吃掉）。
        /// </summary>
        public static string QuoteForWindows(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(QuoteOneForWindows(a));
            }
            return sb.ToString();
        }

        public static string QuoteOneForWindows(string s)
        {
            if (s == null) s = string.Empty;
            if (s.Length > 0 && s.IndexOf(' ') < 0 && s.IndexOf('"') < 0 && s.IndexOf('\t') < 0)
                return s;

            var sb = new StringBuilder();
            sb.Append('"');
            int backslashes = 0;
            foreach (char c in s)
            {
                if (c == '\\')
                {
                    backslashes++;
                }
                else if (c == '"')
                {
                    // 反斜杠先翻倍，再给 " 加转义反斜杠
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    sb.Append('\\', backslashes);
                    backslashes = 0;
                    sb.Append(c);
                }
            }
            // 收尾：反斜杠翻倍，避免把结束引号吃掉（C:\x\ → "C:\x\\"）
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// POSIX（macOS 分支，交给 /bin/bash -cl）：每个参数用 ' 包裹，
        /// 内部 ' 替换为 '\''。不依赖 ~ 展开。
        /// </summary>
        public static string QuoteForPosix(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(QuoteOneForPosix(a));
            }
            return sb.ToString();
        }

        public static string QuoteOneForPosix(string s)
        {
            if (s == null) s = string.Empty;
            return "'" + s.Replace("'", "'\\''") + "'";
        }
    }
}
