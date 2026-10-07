# 行尾探测：报告每个文件是 CRLF 还是 LF（子代理编辑时不得改变行尾）
param([string]$Root = "Assets/Scripts")
$files = Get-ChildItem -Path $Root -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\Generated\\|\\Luban' }
$crlf = 0; $lf = 0; $mixed = @()
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $n = 0; $r = 0
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 10) { $n++; if ($i -gt 0 -and $bytes[$i - 1] -eq 13) { $r++ } }
    }
    if ($n -eq 0) { continue }
    if ($r -eq $n) { $crlf++ } elseif ($r -eq 0) { $lf++ } else { $mixed += $f.FullName }
}
Write-Output "CRLF=$crlf  LF=$lf  MIXED=$($mixed.Count)"
$mixed | ForEach-Object { Write-Output "MIXED: $_" }
