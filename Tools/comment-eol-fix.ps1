# 按基线快照恢复某个文件原先的行尾风格（write 工具会把 CRLF 归一成 LF）。
param(
    [Parameter(Mandatory = $true)][string]$Files,
    [string]$BaselineDir = "$env:TEMP\ds_snap0"
)
$ErrorActionPreference = 'Stop'
foreach ($rel in ($Files -split ';' | Where-Object { $_.Trim() })) {
    $rel = $rel.Trim()
    $cur = Join-Path (Get-Location) $rel
    $base = Join-Path $BaselineDir $rel
    function EolOf([string]$p) {
        $b = [System.IO.File]::ReadAllBytes($p)
        $n = 0; $r = 0
        for ($i = 0; $i -lt $b.Length; $i++) { if ($b[$i] -eq 10) { $n++; if ($i -gt 0 -and $b[$i - 1] -eq 13) { $r++ } } }
        if ($n -eq 0) { 'EMPTY' } elseif ($r -eq $n) { 'CRLF' } elseif ($r -eq 0) { 'LF' } else { 'MIXED' }
    }
    $want = EolOf $base
    $have = EolOf $cur
    if ($want -eq $have) { Write-Output "SKIP $rel ($have)"; continue }
    if ($want -ne 'CRLF' -or $have -ne 'LF') {
        Write-Output "MANUAL $rel : 基线=$want 现在=$have （本脚本只处理 LF→CRLF）"; continue
    }
    $text = [System.IO.File]::ReadAllText($cur)
    $text = $text -replace "`r`n", "`n"
    $text = $text -replace "`n", "`r`n"
    [System.IO.File]::WriteAllText($cur, $text, [System.Text.UTF8Encoding]::new($false))
    Write-Output "FIXED $rel : LF → CRLF"
}
