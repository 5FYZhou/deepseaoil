# 本次改动的合规判据（两条一起看）：
#  ① 改后文件的非注释行序列必须是改前序列的子序列（不许改代码 / 加代码）。
#  ② 被删掉的「非注释行」必须全部是空行（删掉的只能落在注释与空白上）。
#  ③ 行尾风格不得变化。
param(
    [string]$BaselineDir = "$env:TEMP\ds_snap0",
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
        if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { continue }
        $line = $l -replace '/\*.*?\*/', ''
        $idx = $line.IndexOf('//')
        if ($idx -ge 0) { $line = $line.Substring(0, $idx) }
        $out.Add($line.TrimEnd())
    }
    return $out
}
function Get-Eol([string]$path) {
    $b = [System.IO.File]::ReadAllBytes($path)
    $n = 0; $r = 0
    for ($i = 0; $i -lt $b.Length; $i++) {
        if ($b[$i] -eq 10) { $n++; if ($i -gt 0 -and $b[$i - 1] -eq 13) { $r++ } }
    }
    if ($n -eq 0) { return 'EMPTY' } elseif ($r -eq $n) { return 'CRLF' } elseif ($r -eq 0) { return 'LF' } else { return 'MIXED' }
}
$viol = @(); $checked = 0; $cutTotal = 0
foreach ($rel in $list) {
    $cur = Join-Path (Get-Location) $rel
    $base = Join-Path $BaselineDir $rel
    if (-not (Test-Path -LiteralPath $cur) -or -not (Test-Path -LiteralPath $base)) {
        $viol += [pscustomobject]@{ File = $rel; Why = 'MISSING' }; continue
    }
    $checked++
    $old = Get-CodeLines $base
    $new = Get-CodeLines $cur
    if ($new.Count -gt $old.Count) { $viol += [pscustomobject]@{ File = $rel; Why = "非注释行变多 $($old.Count)→$($new.Count)" }; continue }
    # ① 子序列
    $i = 0; $ok = $true
    foreach ($line in $new) {
        while ($i -lt $old.Count -and $old[$i] -ne $line) { $i++ }
        if ($i -ge $old.Count) { $viol += [pscustomobject]@{ File = $rel; Why = "新增/改动的非注释行: $line" }; $ok = $false; break }
        $i++
    }
    if (-not $ok) { continue }
    # ② 补集必须是空行
    $j = 0
    for ($k = 0; $k -lt $old.Count; $k++) {
        if ($j -lt $new.Count -and $old[$k] -eq $new[$j]) { $j++; continue }
        if ($old[$k].Trim().Length -ne 0) {
            $viol += [pscustomobject]@{ File = $rel; Why = "删掉了非空非注释行(旧行 $($k+1)): $($old[$k])" }
        }
        $cutTotal++
    }
    # ③ 行尾
    $e1 = Get-Eol $base; $e2 = Get-Eol $cur
    if ($e1 -ne $e2) { $viol += [pscustomobject]@{ File = $rel; Why = "行尾风格变化 $e1 → $e2" } }
}
Write-Output "CHECKED=$checked  删除的非注释行=$cutTotal  违规=$($viol.Count)"
if ($viol.Count -gt 0) {
    $viol | ForEach-Object { Write-Output ("VIOLATION {0}`n    {1}" -f $_.File, $_.Why) }
    exit 1
}
Write-Output "PASS: 非注释行未改、删除项全为空行、行尾风格未变"
exit 0
