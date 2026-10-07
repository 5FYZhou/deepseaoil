<#
编译门（不依赖 Unity，只依赖本机 dotnet + Unity 安装目录里的 DLL）。

为什么需要它：
  Assets/Scripts 下没有 asmdef，全部落 Assembly-CSharp；而 *.csproj 只由 Unity 在编辑器里
  重新生成。改代码时一旦新增/删除/改名文件，仓库里那份 Assembly-CSharp.csproj 的
  <Compile Include> 清单就是过期的 —— 直接 build 它会漏编译新文件，
  或因为清单里的旧文件不存在而报错。
  本脚本因此照着仓库里那两份 csproj **现场生成** glob 版临时工程（落在 Temp/compile-gate/，
  被 .gitignore 覆盖；不进 Unity、不进构建产物）：

    Gate.csproj       ← Assets/Scripts/** ＋ Assets/Tests/Scene/**    （对应 Assembly-CSharp）
    GateEditor.csproj ← Assets/Tests/Runtime/Editor/**               （对应 Assembly-CSharp-Editor）

  只改四件事：① <Compile> 清单换成 glob；② HintPath 改成绝对路径（临时工程不在仓库根）；
  ③ ProjectReference 改成绝对路径并把运行时工程指向 Gate.csproj；④ AssemblyName 改名，
  避免覆盖 Unity 自己的 Temp/bin 输出。

用法：
  pwsh Tools/compile-gate.ps1              # 生成 + 编译
  pwsh Tools/compile-gate.ps1 -NoBuild     # 只生成工程

退出码：0 = 0 error；1 = 有 error 或前置检查失败。
基线（2026-10-05，批 0）：0 error / 1 warning（SaveService.cs CS0649，既有）。
警告计数口径（2026-10-07 修正）：数的是**去重后的逐条诊断**（`路径(行,列): warning CSxxxx: 消息`）。
  两个坑各踩过一次：① 汇总行 `    1 个警告` 的缩进里含 `: `，被旧写法算成了诊断；
  ② `dotnet build` 把同一条诊断打印两遍（编译阶段一次、末尾汇总段一次）。
  症状都是"代码没动、门的数字变了"，而那种数字不可用于验收。见脚本末尾的计数函数。
#>
[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$outDir  = Join-Path $root 'Temp/compile-gate'
$utf8    = [System.Text.UTF8Encoding]::new($false)

function New-GateProject {
    param(
        [string]$SourceProject,
        [string]$TargetProject,
        [string]$AssemblyName,
        [string[]]$Globs,
        [hashtable]$ProjectRefOverrides
    )

    if (-not (Test-Path $SourceProject)) { throw "找不到源工程：$SourceProject" }

    $text = Get-Content -Raw -LiteralPath $SourceProject

    # ① 去掉显式源码清单（改由 glob 覆盖）
    $text = $text -replace '(?m)^\s*<Compile Include="[^"]+" />\r?\n', ''

    # ② HintPath 绝对化
    $text = [regex]::Replace($text, '<HintPath>([^<]+)</HintPath>', {
            param($m)
            $p = $m.Groups[1].Value
            if ([System.IO.Path]::IsPathRooted($p)) { return $m.Value }
            return '<HintPath>' + (Join-Path $root $p) + '</HintPath>'
        })

    # ③ ProjectReference 绝对化（并允许改指向，例如运行时工程 → Gate.csproj）
    $text = [regex]::Replace($text, '<ProjectReference Include="([^"]+)" />', {
            param($m)
            $k = $m.Groups[1].Value
            if ($ProjectRefOverrides.ContainsKey($k)) { return '<ProjectReference Include="' + $ProjectRefOverrides[$k] + '" />' }
            return '<ProjectReference Include="' + (Join-Path $root $k) + '" />'
        })

    # ④ 改程序集名，输出不与 Unity 自己的 Temp/bin 打架
    $text = $text -replace '<AssemblyName>[^<]+</AssemblyName>', "<AssemblyName>$AssemblyName</AssemblyName>"

    # ⑤ 在 Sdk.props 之后插入 glob 源码组
    $globXml = ($Globs | ForEach-Object { '    <Compile Include="' + $_ + '" />' }) -join "`n"
    $anchor  = '<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />'
    if ($text -notmatch [regex]::Escape($anchor)) { throw "源工程结构变了（找不到 Sdk.props 导入）：$SourceProject" }
    $text = $text -replace [regex]::Escape($anchor), ($anchor + "`n  <ItemGroup>`n" + $globXml + "`n  </ItemGroup>")

    [System.IO.File]::WriteAllText($TargetProject, $text, $utf8)

    # 核对：glob 之外不得再有显式 Compile（否则说明清单没被清干净）
    $leftover = ([regex]::Matches($text, '<Compile Include="(?!\.\.)')).Count
    Write-Host ("  生成 {0}：{1} 个 glob，残留显式清单 {2} 条" -f [System.IO.Path]::GetFileName($TargetProject), $Globs.Count, $leftover)
    if ($leftover -ne 0) { throw "源码清单没清干净（$TargetProject）：仍有 $leftover 条显式 <Compile>" }
}

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

Write-Host '生成编译门工程：' -ForegroundColor Cyan

$gatePath       = Join-Path $outDir 'Gate.csproj'
$gateEditorPath = Join-Path $outDir 'GateEditor.csproj'

$runtimeOverrides = @{
    'Assembly-CSharp.csproj' = $gatePath
}

New-GateProject `
    -SourceProject (Join-Path $root 'Assembly-CSharp.csproj') `
    -TargetProject $gatePath `
    -AssemblyName 'CompileGate' `
    -Globs @('..\..\Assets\Scripts\**\*.cs', '..\..\Assets\Tests\Scene\**\*.cs') `
    -ProjectRefOverrides $runtimeOverrides

New-GateProject `
    -SourceProject (Join-Path $root 'Assembly-CSharp-Editor.csproj') `
    -TargetProject $gateEditorPath `
    -AssemblyName 'CompileGateEditor' `
    -Globs @('..\..\Assets\Tests\Runtime\Editor\**\*.cs') `
    -ProjectRefOverrides @{ 'Assembly-CSharp.csproj' = $gatePath }

if ($NoBuild) { Write-Host '（-NoBuild：跳过编译）'; exit 0 }

Write-Host '编译：' -ForegroundColor Cyan
$log = & dotnet build $gateEditorPath -v minimal -nologo 2>&1
$log | ForEach-Object { Write-Host $_ }

# 计数口径：**去重后的逐条诊断**。
# 两个坑各踩过一次：
#   ① 汇总行 `    1 个警告` 的缩进里含 `: `，被旧的 `: warning [A-Z]+\d+` 数成了诊断；
#   ② `dotnet build` 把同一条诊断**打印两遍**（编译阶段一次、末尾汇总段一次），
#      所以"匹配到几行"不等于"有几条警告"。
# 于是这里先按「路径(行,列) ＋ 级别 ＋ 编号」去重，再数。
function Get-DiagnosticCount {
    param(
        [object[]]$Log,
        [string]$Level
    )

    $seen = @{}

    foreach ($line in $Log) {
        $m = [regex]::Match([string]$line, '^(.*?\(\d+,\d+\)): ' + $Level + ' ([A-Z]+\d+):')

        if ($m.Success) { $seen[$m.Groups[1].Value + '|' + $m.Groups[2].Value] = $true }
    }

    return $seen.Count
}

$errors   = Get-DiagnosticCount -Log $log -Level 'error'
$warnings = Get-DiagnosticCount -Log $log -Level 'warning'

Write-Host ''
Write-Host ("错误 {0} 条 / 警告 {1} 条" -f $errors, $warnings) -ForegroundColor $(if ($errors -eq 0) { 'Green' } else { 'Red' })

if ($errors -gt 0) { exit 1 }
exit 0
