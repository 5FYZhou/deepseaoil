// ---------------------------------------------------------------------------
// 静态检查（DeepseaOil）
//
// 【职责边界】真实语义编译交给 Unity 生成的 .csproj（见 Tools/audit.ps1 -Full），
//   因为只有它们带着与 Unity 完全一致的引用集与 120+ 个预定义宏。
//   本工具只做 .csproj 查不出来的两类事：
//     ① NUnit 断言重载 —— Assert.AreEqual(Vector2, Vector2, double) 这种写法
//        在 Unity 里报 CS1503，但错误信息很难看出根因，这里给出人话解释
//     ② 菜单路径一致性 —— [MenuItem] 的名字与文档/校验器里引用的名字对不上
//
// 【断言规则怎么做到准确】不靠正则猜：现场给编译补一份 NUnit 的 Assert 声明桩
//   （与 4.6 的重载形状一致），Roslyn 于是能真正做重载解析；再复核一遍
//   "解析到的重载里有没有把复杂类型隐式转成 double" —— 那正是 CS1503 的成因。
//
// 跑法：pwsh Tools/audit.ps1
// 退出码：0 = 无 FAIL；1 = 有 FAIL
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DeepseaOil.Audit
{
    internal static class Program
    {
        private static readonly string[] SkipSegments =
        {
            "\\Library\\", "\\Temp\\", "\\obj\\", "\\bin\\", "\\.git\\",
            "\\Tools\\Audit\\", "\\StreamingAssets\\",
        };

        /// <summary>只查这些断言方法：它们的容差重载最容易踩错。</summary>
        private static readonly HashSet<string> NumericAssertions = new(StringComparer.Ordinal)
        {
            "AreEqual", "AreNotEqual",
            "Greater", "GreaterOrEqual", "Less", "LessOrEqual",
        };

        /// <summary>
        /// NUnit 的 Assert 声明桩：只为让 Roslyn 解析出重载，不参与运行。
        /// 形状与 NUnit 4.6 的 Assert.Static.cs 一致（含 double 容差族与 IComparable 族）。
        /// </summary>
        private const string NUnitAssertStub = @"
namespace NUnit.Framework
{
    public class Assert
    {
        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message, params object[] args) { }
        public static void AreEqual(double expected, double actual, double delta) { }
        public static void AreEqual(double expected, double actual, double delta, string message, params object[] args) { }
        public static void AreEqual(System.IComparable expected, System.IComparable actual) { }
        public static void AreEqual(System.IComparable expected, System.IComparable actual, string message, params object[] args) { }
        public static void AreNotEqual(object expected, object actual) { }
        public static void AreNotEqual(object expected, object actual, string message, params object[] args) { }
        public static void AreNotEqual(double expected, double actual, double delta) { }
        public static void AreNotEqual(double expected, double actual, double delta, string message, params object[] args) { }
        public static void AreNotEqual(System.IComparable expected, System.IComparable actual) { }
        public static void AreNotEqual(System.IComparable expected, System.IComparable actual, string message, params object[] args) { }
        public static void Greater(int arg1, int arg2) { }
        public static void Greater(uint arg1, uint arg2) { }
        public static void Greater(long arg1, long arg2) { }
        public static void Greater(ulong arg1, ulong arg2) { }
        public static void Greater(decimal arg1, decimal arg2) { }
        public static void Greater(double arg1, double arg2) { }
        public static void Greater(float arg1, float arg2) { }
        public static void Greater(System.IComparable arg1, System.IComparable arg2) { }
        public static void Greater(int arg1, int arg2, string message, params object[] args) { }
        public static void Greater(uint arg1, uint arg2, string message, params object[] args) { }
        public static void Greater(long arg1, long arg2, string message, params object[] args) { }
        public static void Greater(ulong arg1, ulong arg2, string message, params object[] args) { }
        public static void Greater(decimal arg1, decimal arg2, string message, params object[] args) { }
        public static void Greater(double arg1, double arg2, string message, params object[] args) { }
        public static void Greater(float arg1, float arg2, string message, params object[] args) { }
        public static void Greater(System.IComparable arg1, System.IComparable arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(int arg1, int arg2) { }
        public static void GreaterOrEqual(uint arg1, uint arg2) { }
        public static void GreaterOrEqual(long arg1, long arg2) { }
        public static void GreaterOrEqual(ulong arg1, ulong arg2) { }
        public static void GreaterOrEqual(decimal arg1, decimal arg2) { }
        public static void GreaterOrEqual(double arg1, double arg2) { }
        public static void GreaterOrEqual(float arg1, float arg2) { }
        public static void GreaterOrEqual(System.IComparable arg1, System.IComparable arg2) { }
        public static void GreaterOrEqual(int arg1, int arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(uint arg1, uint arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(long arg1, long arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(ulong arg1, ulong arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(decimal arg1, decimal arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(double arg1, double arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(float arg1, float arg2, string message, params object[] args) { }
        public static void GreaterOrEqual(System.IComparable arg1, System.IComparable arg2, string message, params object[] args) { }
        public static void Less(int arg1, int arg2) { }
        public static void Less(uint arg1, uint arg2) { }
        public static void Less(long arg1, long arg2) { }
        public static void Less(ulong arg1, ulong arg2) { }
        public static void Less(decimal arg1, decimal arg2) { }
        public static void Less(double arg1, double arg2) { }
        public static void Less(float arg1, float arg2) { }
        public static void Less(System.IComparable arg1, System.IComparable arg2) { }
        public static void Less(int arg1, int arg2, string message, params object[] args) { }
        public static void Less(uint arg1, uint arg2, string message, params object[] args) { }
        public static void Less(long arg1, long arg2, string message, params object[] args) { }
        public static void Less(ulong arg1, ulong arg2, string message, params object[] args) { }
        public static void Less(decimal arg1, decimal arg2, string message, params object[] args) { }
        public static void Less(double arg1, double arg2, string message, params object[] args) { }
        public static void Less(float arg1, float arg2, string message, params object[] args) { }
        public static void Less(System.IComparable arg1, System.IComparable arg2, string message, params object[] args) { }
    }
}
";

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== 深海石油 · 静态检查（语法 / 断言规则 / 菜单一致性）===");

            string repoRoot = FindRepoRoot();
            if (repoRoot == null)
            {
                Console.Error.WriteLine("找不到仓库根（向上没有 Assets 目录）。请在仓库内运行。");
                return 1;
            }

            List<string> sources = CollectSources(Path.Combine(repoRoot, "Assets"));
            Console.WriteLine("仓库根: " + repoRoot);
            Console.WriteLine("待解析 C# 文件: " + sources.Count);

            var parseOptions = ParseOptions();
            var trees = new List<SyntaxTree>(sources.Count + 1)
            {
                CSharpSyntaxTree.ParseText(NUnitAssertStub, parseOptions, "<NUnit.Assert 声明桩>"),
            };

            int syntaxErrors = 0;

            foreach (string file in sources)
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(ReadAllTextWithRetry(file), parseOptions, file);
                trees.Add(tree);

                foreach (Diagnostic diagnostic in tree.GetDiagnostics())
                {
                    if (diagnostic.Severity != DiagnosticSeverity.Error) continue;

                    FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                    syntaxErrors++;
                    Console.WriteLine($"FAIL 语法 {Short(span.Path)}({span.StartLinePosition.Line + 1}): {diagnostic.GetMessage()}");
                }
            }

            var compilation = CSharpCompilation.Create(
                "DeepseaOil.StaticCheck",
                trees,
                Array.Empty<MetadataReference>(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            int assertIssues = RunAssertionRules(compilation);
            int menuIssues = RunMenuConsistency(repoRoot);

            Console.WriteLine();
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine($"语法错误: {syntaxErrors}   断言规则: {assertIssues}   菜单一致性: {menuIssues}");

            int failures = syntaxErrors + assertIssues + menuIssues;
            if (failures == 0)
            {
                Console.WriteLine("静态检查通过。");
                return 0;
            }

            Console.WriteLine($"静态检查未通过：{failures} 项。");
            return 1;
        }

        // ------------------------------------------------------------ 断言规则

        private static int RunAssertionRules(CSharpCompilation compilation)
        {
            int issues = 0;

            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                string path = tree.FilePath;
                if (path != null && path.StartsWith("<", StringComparison.Ordinal)) continue;

                SemanticModel model = compilation.GetSemanticModel(tree);

                foreach (InvocationExpressionSyntax invocation in tree.GetRoot()
                             .DescendantNodes()
                             .OfType<InvocationExpressionSyntax>())
                {
                    if (!(invocation.Expression is MemberAccessExpressionSyntax member)) continue;

                    string methodName = member.Name.Identifier.ValueText;
                    if (!NumericAssertions.Contains(methodName)) continue;

                    string owner = model.GetTypeInfo(member.Expression).Type?.Name;
                    if (owner != "Assert") continue;

                    // 少于两个实参的调用形状（如 Assert.Fail(msg)）不归本规则管
                    if (invocation.ArgumentList.Arguments.Count < 2) continue;

                    if (IsWellFormed(model, invocation, out string reason)) continue;

                    FileLinePositionSpan span = invocation.GetLocation().GetLineSpan();
                    issues++;
                    Console.WriteLine(
                        $"FAIL 断言 {Short(span.Path)}({span.StartLinePosition.Line + 1}): Assert.{methodName} {reason}");                }
            }

            if (issues > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  提示：NUnit 的 Assert.AreEqual 只有 (期望, 实际)、(期望, 实际, 消息)、");
                Console.WriteLine("        (double, double, double) 与 (double, double, double, 消息) 这几种形状。");
                Console.WriteLine("        要带容差比较向量，请写 Assert.Less(Vector2.Distance(a, b), 容差, 消息)。");
            }

            return issues;
        }

        /// <summary>
        /// 返回 true 表示这次调用看着没问题。
        /// 先走语义判定；语义判断不了时（本工具没有 Unity 引用集，多数实参类型解析不出来）
        /// 退回一条结构规则，专门抓"第三个实参是数字字面量"这种形状错误。
        /// </summary>
        private static bool IsWellFormed(SemanticModel model, InvocationExpressionSyntax invocation, out string reason)
        {
            reason = null;

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

            var argumentTypes = new List<ITypeSymbol>(arguments.Count);
            foreach (ArgumentSyntax argument in arguments)
            {
                TypeInfo info = model.GetTypeInfo(argument.Expression);
                argumentTypes.Add(info.ConvertedType ?? info.Type);
            }

            // 有解析不出来的实参就不下语义结论，避免误报
            bool typesResolved = true;
            foreach (ITypeSymbol type in argumentTypes)
            {
                if (type == null || type.TypeKind == TypeKind.Error) typesResolved = false;
            }

            // 枚举比较（例如 MovementStateTag）不在语义规则射程内：
            // 实参类型解析不出来时 ConvertedType 会退化成 object，规则会把合法写法误判成错误。
            if (typesResolved && arguments.Count >= 2)
            {
                foreach (ArgumentSyntax argument in arguments.Take(2))
                {
                    if (model.GetTypeInfo(argument.Expression).Type?.TypeKind == TypeKind.Enum) return true;
                }
            }

            // 语义判定只在类型都解析出来时才可信；它说不行就直接报它给的原因
            if (typesResolved && !IsSemanticallyWellFormed(model, invocation, argumentTypes, out reason))
            {
                return false;
            }

            // 结构规则独立生效，不做"语义说行就放过"的短路：
            // 本工具没有 Unity 引用集时，语义常常给出"看着行"的假绿，正是靠这条兜住。
            return IsStructurallyWellFormed(arguments, out reason);
        }

        private static bool IsSemanticallyWellFormed(
            SemanticModel model,
            InvocationExpressionSyntax invocation,
            List<ITypeSymbol> argumentTypes,
            out string reason)
        {
            reason = null;
            SymbolInfo symbolInfo = model.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is IMethodSymbol resolved && resolved.Parameters.Length == argumentTypes.Count)
            {
                // 解析成功但发生了"复杂类型 → 浮点"的隐式转换：这就是 CS1503 的成因
                for (int i = 0; i < argumentTypes.Count; i++)
                {
                    ITypeSymbol parameterType = resolved.Parameters[i].Type;
                    if (!IsFloating(parameterType)) continue;
                    if (IsNumeric(argumentTypes[i])) continue;

                    reason = $"第 {i + 1} 个实参是 {argumentTypes[i].Name}，却会隐式转成 {parameterType.Name} —— NUnit 没有匹配的重载";
                    return false;
                }

                return true;
            }

            // 解析失败：只要还有非 params 候选的形参个数对得上，就说明是类型不匹配而非形状不对
            foreach (IMethodSymbol candidate in symbolInfo.CandidateSymbols.OfType<IMethodSymbol>())
            {
                if (candidate.Parameters.Length != argumentTypes.Count) continue;
                if (candidate.Parameters.Length > 0 && candidate.Parameters[candidate.Parameters.Length - 1].IsParams) continue;

                reason = "解析不到匹配的重载（实参类型与 NUnit 的任何重载都不符）";
                return false;
            }

            if (symbolInfo.CandidateSymbols.Length == 0)
            {
                reason = "解析不到任何 NUnit 重载（实参形状不对）";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 只在语义解析不出来时兜底的结构规则（本工具没有 Unity 引用集时是常态）。
        /// 判定刻意收窄到"能确定的错误"：容差实参是数字字面量，且前两个实参里有一个明显是复杂类型。
        /// 认不出的写法一律放过 —— 漏掉的错误 -Full 的真实语义编译一定会报出来。
        /// </summary>
        private static bool IsStructurallyWellFormed(SeparatedSyntaxList<ArgumentSyntax> arguments, out string reason)
        {
            reason = null;

            if (arguments.Count < 3) return true;

            ExpressionSyntax third = arguments[2].Expression;

            // 只在"第三个实参确定是数字"时才判定，避免把消息字符串当成容差
            bool numericThird = third is LiteralExpressionSyntax literal
                                && literal.IsKind(SyntaxKind.NumericLiteralExpression);
            if (!numericThird) return true;

            if (!IsClearlyComplex(arguments[0].Expression) && !IsClearlyComplex(arguments[1].Expression)) return true;

            reason = "第三个实参是数字字面量，会被当成容差 delta，于是前两个实参必须能转成 double"
                     + " —— NUnit 没有 (任意类型, 任意类型, double) 这个重载"
                     + "（带容差比较向量请写 Assert.Less(Vector2.Distance(a, b), 容差, 消息)）";
            return false;
        }

        /// <summary>
        /// 粗判一个实参"明显不是 double"。
        /// 只认建设性写法（对象创建、字符串、复合字面量）；变量与方法调用一律不猜。
        /// </summary>
        private static bool IsClearlyComplex(ExpressionSyntax expression)
        {
            switch (expression)
            {
                case ObjectCreationExpressionSyntax:
                    return true;

                case ImplicitObjectCreationExpressionSyntax:
                    return true;

                case LiteralExpressionSyntax literal:
                    // 字符串字面量、"null" 这类明显不是数值的
                    return !literal.IsKind(SyntaxKind.NumericLiteralExpression)
                           && !literal.IsKind(SyntaxKind.TrueLiteralExpression)
                           && !literal.IsKind(SyntaxKind.FalseLiteralExpression);

                case ArrayCreationExpressionSyntax:
                    return true;

                case TypeOfExpressionSyntax:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsFloating(ITypeSymbol type)
        {
            return type.SpecialType is SpecialType.System_Double or SpecialType.System_Single;
        }

        private static bool IsNumeric(ITypeSymbol type)
        {
            return type.SpecialType is
                SpecialType.System_Double or
                SpecialType.System_Single or
                SpecialType.System_Int32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt32 or
                SpecialType.System_UInt64 or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_Byte or
                SpecialType.System_SByte or
                SpecialType.System_Decimal;
        }

        // ------------------------------------------------------------ 菜单一致性

        private static int RunMenuConsistency(string repoRoot)
        {
            var declared = new SortedSet<string>(StringComparer.Ordinal);
            var referenced = new SortedSet<string>(StringComparer.Ordinal);

            string[] probeFiles =
            {
                Path.Combine(repoRoot, "Assets", "Tests", "Tools", "仓库一致性Tests.cs"),
                Path.Combine(repoRoot, "Tools", "check-tree.mjs"),
                Path.Combine(repoRoot, "Docs", "分层设计", "表现层.md"),
            };

            foreach (string file in probeFiles)
            {
                if (!File.Exists(file)) continue;

                foreach (Match match in Regex.Matches(
                             ReadAllTextWithRetry(file),
                             @"Tools[/\\▸ ]*深海石油[/\\▸ ]*([^\s""'`<>\r\n|]+)"))
                {
                    string name = match.Groups[1].Value.TrimEnd('。', '，', '.', ',', ')', '）', '、');
                    if (name.Length > 0) referenced.Add(name);
                }
            }

            foreach (string source in CollectSources(Path.Combine(repoRoot, "Assets")))
            {
                foreach (Match match in Regex.Matches(
                             ReadAllTextWithRetry(source),
                             @"\[MenuItem\(""Tools/深海石油/([^""]+)"""))
                {
                    declared.Add(match.Groups[1].Value.Trim());
                }
            }

            Console.WriteLine();
            Console.WriteLine("菜单声明: " + (declared.Count == 0 ? "（无）" : string.Join(" | ", declared)));

            if (referenced.Count == 0) return 0;

            Console.WriteLine("菜单被引用: " + string.Join(" | ", referenced));

            int issues = 0;
            foreach (string name in referenced)
            {
                if (declared.Contains(name)) continue;

                issues++;
                Console.WriteLine($"FAIL 菜单 被文档或校验器引用，但代码里没有: Tools ▸ 深海石油 ▸ {name}");
            }

            return issues;
        }

        // ------------------------------------------------------------ 公共设施

        private static CSharpParseOptions ParseOptions()
        {
            return new CSharpParseOptions(
                LanguageVersion.CSharp9,
                DocumentationMode.None,
                SourceCodeKind.Regular,
                new[]
                {
                    "UNITY_EDITOR",
                    "UNITY_2022_3_OR_NEWER",
                    "UNITY_STANDALONE_WIN",
                    "UNITY_STANDALONE",
                    "DEBUG",
                    "ENABLE_INPUT_SYSTEM",
                });
        }

        private static List<string> CollectSources(string assetsRoot)
        {
            var result = new List<string>();
            if (!Directory.Exists(assetsRoot)) return result;

            foreach (string file in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (SkipSegments.Any(segment => file.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                result.Add(file);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets"))) return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }

        private static string ReadAllTextWithRetry(string path)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch (IOException)
                {
                    Thread.Sleep(40);
                }
            }

            return File.ReadAllText(path);
        }

        private static string Short(string path)
        {
            if (string.IsNullOrEmpty(path)) return "<未知>";

            int index = path.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? path.Substring(index) : path;
        }
    }
}
