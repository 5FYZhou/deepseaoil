# ---------------------------------------------------------------------------
# 交付前门禁
#
#   pwsh Tools/audit.ps1            # 快：语法 + 断言规则 + 菜单一致性
#   pwsh Tools/audit.ps1 -Full      # 全：再加两遍真实语义编译（交 Unity 前必跑）
#
# 语义级直接编译 Unity 自己生成的 Assembly-CSharp.csproj / Assembly-CSharp-Editor.csproj：
#   它们带着与 Unity 完全相同的引用集与 120+ 个预定义宏（UNITY_EDITOR / UNITY_2022_3_62 …），
#   所以查出来的错误就是 Unity Console 里会出现的错误，不会多也不会少。
#   自己拿 Roslyn 拼引用集是行不通的：会缺核心库、并把各程序集混在一起编，产出上万条假错误。
#
# 退出码：0 = 全绿；1 = 有 FAIL
# ---------------------------------------------------------------------------
[CmdletBinding()]
param(
    [switch]$Full,
    [switch]$LintOnly
)

$ErrorActionPreference = 'Stop'

if ($Full -and $LintOnly) { $Full = $false }

$repoRoot = Split-Path $PSScriptRoot -Parent
$auditProject = Join-Path $PSScriptRoot 'Audit/DeepseaOil.Audit.csproj'

$unityProjects = @(
    (Join-Path $repoRoot 'Assembly-CSharp.csproj'),
    (Join-Path $repoRoot 'Assembly-CSharp-Editor.csproj')
)

$failed = 0
$notes = New-Object System.Collections.Generic.List[string]

function Write-Section([string]$title) {
    Write-Host ''
    Write-Host ("=== " + $title + " ===")
}

# --------------------------------------------------------------- 1. 静态检查

Write-Section '静态检查（语法 + 断言规则 + 菜单一致性）'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error '找不到 dotnet SDK。语义级与静态检查都需要它（工程用 net9.0）。'
    exit 1
}

if (-not (Test-Path $auditProject)) {
    Write-Error "找不到门禁工程：$auditProject"
    exit 1
}

Push-Location $repoRoot
try {
    $lintArgs = @('run', '--project', $auditProject, '-c', 'Release', '--nologo', '--verbosity', 'quiet', '--no-restore')

    # 先试"不还原"的快路径；失败通常只是缺包，补一次 restore 再重试。
    # 中间的失败输出不打印，避免把一次可恢复的缺包刷成一片红。
    $lintOutput = & dotnet @lintArgs 2>&1
    $lintCode = $LASTEXITCODE

    if ($lintCode -ne 0) {
        Write-Host '（首次运行，正在还原静态检查工具的依赖…）'
        & dotnet restore $auditProject | Out-Null

        if ($LASTEXITCODE -ne 0) {
            Write-Error 'dotnet restore 失败。首次运行需要联网做一次还原。'
            exit 1
        }

        $lintOutput = & dotnet @lintArgs 2>&1
        $lintCode = $LASTEXITCODE
    }

    $lintOutput | ForEach-Object { Write-Host $_ }
    if ($lintCode -ne 0) { $failed++ }

    # ----------------------------------------------------------- 2. 语义编译

    if ($Full) {
        Write-Section '语义编译（Unity 生成的工程，引用集与宏与 Unity 一致）'

        $missing = @($unityProjects | Where-Object { -not (Test-Path $_) })
        if ($missing.Count -gt 0) {
            Write-Host '语义级不可用：找不到 Unity 生成的工程文件：'
            foreach ($m in $missing) { Write-Host ('  ' + (Split-Path $m -Leaf)) }
            Write-Host ''
            Write-Host '它们由 Unity 的 IDE 集成在导入脚本时生成，且被 .gitignore 排除（*.csproj）。'
            Write-Host '恢复办法：在 Unity 里 Edit ▸ Preferences ▸ External Tools 勾上'
            Write-Host '  "Generate .csproj files for: Embedded packages / Local packages / Registry packages"'
            Write-Host '然后随便改一个 .cs 触发一次重编译即可。'
            Write-Host ''
            Write-Host '注意：跳过语义级时，CS0246 / CS0534 / CS1503 这类错误查不出来。'
            $notes.Add('语义级被跳过（缺 Unity 生成的 .csproj）')
        }
        else {
            foreach ($project in $unityProjects) {
                Write-Host ''
                Write-Host ('--- ' + (Split-Path $project -Leaf) + ' ---')

                $output = & dotnet build $project -v quiet --nologo 2>&1
                $code = $LASTEXITCODE

                $errors = @($output | Where-Object { $_ -match 'error|错误' })

                if ($code -eq 0) {
                    Write-Host '  0 个错误'
                }
                else {
                    $errors | Select-Object -First 40 | ForEach-Object { Write-Host ('  ' + $_) }
                    Write-Host ('  编译失败，退出码 ' + $code)
                    $failed++
                }
            }
        }
    }
    else {
        Write-Section '语义编译'
        Write-Host '已跳过（未加 -Full）。交付或让 Unity 编译之前请跑：pwsh Tools/audit.ps1 -Full'
    }
}
finally {
    Pop-Location
}

# --------------------------------------------------------------- 3. 文档校验

Write-Section '文档校验'
foreach ($tool in @('check-tree.mjs', 'check-links.mjs', 'verify-mermaid.mjs')) {
    $path = Join-Path $PSScriptRoot $tool
    if (-not (Test-Path $path)) { continue }

    $output = & node $path 2>&1
    $code = $LASTEXITCODE

    $summary = ($output | Where-Object { $_ -match 'OK|断链|块数|失败' } | Select-Object -Last 1)
    Write-Host ("  {0,-20} exit={1}  {2}" -f $tool, $code, $summary)

    if ($code -ne 0) { $failed++ }
}

# --------------------------------------------------------------- 汇总

Write-Host ''
Write-Host '============================================'
if ($notes.Count -gt 0) {
    foreach ($note in $notes) { Write-Host ('注意：' + $note) }
}

if ($failed -eq 0) {
    Write-Host '门禁通过。'
    exit 0
}

Write-Host ('门禁未通过：' + $failed + ' 项。修完再交给 Unity。')
exit 1
