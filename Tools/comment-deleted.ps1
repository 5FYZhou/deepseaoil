# 列出「改前有、改后没有」的非注释行（子序列比对的补集）。
# 这些行必须是注释内容（XML 文档标签等），不能是代码。
param(
    [Parameter(Mandatory = $true)][string]$File,
    [string]$BaselineDir = "$env:TEMP\ds_snap0"
)
$ErrorActionPreference = 'Stop'
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
$old = Get-CodeLines (Join-Path $BaselineDir $File)
$new = Get-CodeLines $File
Write-Output "OLD non-comment = $($old.Count) ; NEW non-comment = $($new.Count)"
$j = 0
for ($i = 0; $i -lt $old.Count; $i++) {
    if ($j -lt $new.Count -and $old[$i] -eq $new[$j]) { $j++; continue }
    Write-Output ("DELETED OLD-LINE-{0}: [{1}]" -f ($i + 1), $old[$i])
}
Write-Output "matched_new=$j"
