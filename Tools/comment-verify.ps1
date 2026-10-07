# 非注释行比对：注释行 = TrimStart() 以 // /* * 开头。
# 判据：改后文件的「非注释行序列」必须是改前序列的子序列（允许删注释，不允许改/增代码行）。
param(
    [Parameter(Mandatory = $true)][string]$BaselineDir,
    [string]$Root = "Assets/Scripts"
)
$ErrorActionPreference = 'Stop'
$files = Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\Generated\\|\\Luban' }

function Get-CodeLines([string]$path) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($l in (Get-Content -LiteralPath $path)) {
        $t = $l.TrimStart()
        if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { continue }
        $out.Add($l.TrimEnd())
    }
    return $out
}

# 归一化：去掉路径差异，只比相对路径
$bad = @(); $checked = 0; $missing = @()
foreach ($f in $files) {
    $rel = (Resolve-Path -LiteralPath $f.FullName -Relative)
    $rel = $rel -replace '^\.\\', ''
    $base = Join-Path $BaselineDir $rel
    if (-not (Test-Path -LiteralPath $base)) { $missing += $rel; continue }
    $checked++
    $old = Get-CodeLines $base
    $new = Get-CodeLines $f.FullName
    # 子序列判定（朴素双指针）——new 必须能在 old 中按序找到
    $i = 0
    foreach ($line in $new) {
        while ($i -lt $old.Count -and $old[$i] -ne $line) { $i++ }
        if ($i -ge $old.Count) {
            $bad += [pscustomobject]@{ File = $rel; AddedLine = $line; OldCount = $old.Count; NewCount = $new.Count }
            break
        }
        $i++
    }
}
Write-Output "CHECKED=$checked  MISSING_BASELINE=$($missing.Count)  VIOLATIONS=$($bad.Count)"
foreach ($m in $missing) { Write-Output "MISSING: $m" }
if ($bad.Count -gt 0) {
    Write-Output "---- 非注释行不一致（改后出现了改前没有的行）----"
    $bad | ForEach-Object { Write-Output ("{0}`n    + {1}" -f $_.File, $_.AddedLine) }
    exit 1
}
exit 0
