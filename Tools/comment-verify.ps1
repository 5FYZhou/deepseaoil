# 指定文件的「非注释行子序列」比对（子代理自查用）。
# 判据：改后文件的非注释行序列必须是改前序列的子序列。
# 用法： pwsh -NoProfile -File Tools/comment-verify.ps1 -BaselineDir <快照根> -Files "a.cs;b.cs"
param(
    [Parameter(Mandatory = $true)][string]$BaselineDir,
    [string]$Files = "",
    [switch]$All
)
$ErrorActionPreference = 'Stop'
if ($All) {
    $list = Get-ChildItem -Path "Assets/Scripts" -Recurse -Filter *.cs |
        Where-Object { $_.FullName -notmatch '\\Generated\\|\\Luban' } |
        ForEach-Object { (Resolve-Path -LiteralPath $_.FullName -Relative) -replace '^\.\\', '' }
} else {
    $list = $Files -split ';' | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }
}

function Get-CodeLines([string]$path) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($l in (Get-Content -LiteralPath $path)) {
        $t = $l.TrimStart()
        # 整行注释
        if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { continue }
        # code /* block */  → 块注释可在行内，整段去掉
        $line = $l -replace '/\*.*?\*/', ''
        # code // 行尾注释 → 只比代码 token
        $idx = $line.IndexOf('//')
        if ($idx -ge 0) { $line = $line.Substring(0, $idx) }
        $out.Add($line.TrimEnd())
    }
    return $out
}

$bad = @(); $checked = 0; $missing = @(); $summary = @()
foreach ($rel in $list) {
    $cur = Join-Path (Get-Location) $rel
    if (-not (Test-Path -LiteralPath $cur)) { $missing += "CURRENT MISSING: $rel"; continue }
    $base = Join-Path $BaselineDir $rel
    if (-not (Test-Path -LiteralPath $base)) { $missing += "BASELINE MISSING: $rel"; continue }
    $checked++
    $old = Get-CodeLines $base
    $new = Get-CodeLines $cur
    if ($new.Count -gt $old.Count) {
        $bad += [pscustomobject]@{ File = $rel; Why = "非注释行变多：$($old.Count) → $($new.Count)" }
    }
    $i = 0; $ok = $true
    foreach ($line in $new) {
        while ($i -lt $old.Count -and $old[$i] -ne $line) { $i++ }
        if ($i -ge $old.Count) {
            $bad += [pscustomobject]@{ File = $rel; Why = "出现改前没有的非注释行: $line" }; $ok = $false; break
        }
        $i++
    }
    if ($ok) { $summary += ("OK  {0}  (非注释行 {1} -> {2}, 未变)" -f $rel, $old.Count, $new.Count) }
}
$summary | ForEach-Object { Write-Output $_ }
foreach ($m in $missing) { Write-Output $m }
Write-Output "CHECKED=$checked  VIOLATIONS=$($bad.Count)"
if ($bad.Count -gt 0) {
    Write-Output "---- 违规 ----"
    $bad | ForEach-Object { Write-Output ("{0}`n    {1}" -f $_.File, $_.Why) }
    exit 1
}
Write-Output "PASS: 所有被检查文件的非注释行逐行未变"
exit 0
