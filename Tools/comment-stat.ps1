# 注释统计：注释行 = TrimStart() 以 // /* * 开头
param([string]$Root = "Assets/Scripts", [switch]$Top)
$files = Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\Generated\\|\\Luban' }
$rows = foreach ($f in $files) {
    $lines = Get-Content -LiteralPath $f.FullName
    $total = $lines.Count
    $cmt = 0; $blank = 0
    foreach ($l in $lines) {
        $t = $l.TrimStart()
        if ($t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*')) { $cmt++ }
        elseif ($t.Length -eq 0) { $blank++ }
    }
    [pscustomobject]@{
        Rel   = (Resolve-Path -LiteralPath $f.FullName -Relative)
        Total = $total
        Cmt   = $cmt
        Blank = $blank
        Pct   = if ($total) { [math]::Round(100 * $cmt / $total, 1) } else { 0 }
    }
}
$sumT = ($rows | Measure-Object Total -Sum).Sum
$sumC = ($rows | Measure-Object Cmt -Sum).Sum
$sumB = ($rows | Measure-Object Blank -Sum).Sum
Write-Output "FILES=$($rows.Count) TOTAL=$sumT COMMENTS=$sumC BLANK=$sumB PCT=$([math]::Round(100*$sumC/$sumT,1))%"
if ($Top) {
    $rows | Sort-Object Pct -Descending | Select-Object -First 40 |
        Format-Table Rel, Total, Cmt, Pct, Blank -AutoSize
} else {
    $rows | Sort-Object Pct -Descending | ForEach-Object { "{0}`t{1}`t{2}`t{3}" -f $_.Rel, $_.Total, $_.Cmt, $_.Pct }
}
