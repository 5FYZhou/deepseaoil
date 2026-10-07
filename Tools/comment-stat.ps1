# 注释统计（机器可读）：每行 File|Total|Cmt|Pct|Blank，末行 SUMMARY|...
param([string]$Root = "Assets/Scripts", [double]$Cut = 0)
$files = Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\Generated\\|\\Luban' }
$rows = foreach ($f in $files) {
    $lines = Get-Content -LiteralPath $f.FullName
    $total = $lines.Count; $cmt = 0; $blank = 0
    foreach ($l in $lines) {
        $t = $l.TrimStart()
        if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { $cmt++ }
        elseif ($t.Length -eq 0) { $blank++ }
    }
    $rel = (Resolve-Path -LiteralPath $f.FullName -Relative) -replace '^\.\\', ''
    [pscustomobject]@{ Rel = $rel; Total = $total; Cmt = $cmt; Blank = $blank
        Pct = if ($total) { [math]::Round(100 * $cmt / $total, 1) } else { 0 } }
}
$sumT = ($rows | Measure-Object Total -Sum).Sum
$sumC = ($rows | Measure-Object Cmt -Sum).Sum
$sumB = ($rows | Measure-Object Blank -Sum).Sum
foreach ($r in ($rows | Sort-Object Pct -Descending)) {
    if ($Cut -gt 0 -and $r.Pct -le $Cut) { continue }
    Write-Output ("{0}|{1}|{2}|{3}|{4}" -f $r.Rel, $r.Total, $r.Cmt, $r.Pct, $r.Blank)
}
Write-Output ("SUMMARY|files={0}|total={1}|cmt={2}|blank={3}|pct={4}" -f `
    $rows.Count, $sumT, $sumC, $sumB, [math]::Round(100 * $sumC / $sumT, 1))
# 全仓口径：含 Generated（3601/842）与 Luban.Runtime（4482/345）
Write-Output ("REPO_WIDE|total={0}|cmt={1}|pct={2}" -f ($sumT + 8083), ($sumC + 1187), `
    [math]::Round(100 * ($sumC + 1187) / ($sumT + 8083), 1))
