# 一次性打包「全局光照规划」交给外部 AI 的文档与源码。
# 用法：pwsh Tools/pack-toagent.ps1
# 输出：T:\GAMES\toAgent\ 下全部平铺（无子目录）＋ MANIFEST.txt
# 上传兼容：白名单外的扩展名一律在后面补 .txt（内容一字不改），原格式保留在文件名里。

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dst = 'T:\GAMES\toAgent'

# 这些后缀原样上传；其余（.unity/.prefab/.asset/.mat/.shader/.asmdef/.ps1 …）补 .txt。
$uploadableExt = @('.txt', '.md', '.cs', '.json', '.xml', '.yml', '.yaml', '.csv')

# 分组 → 相对仓库根的路径。路径可写文件或目录（目录整棵复制）。
$groups = [ordered]@{
    '01-纪律与现状' = @(
        'AGENTS.md'
        'Docs/工程/架构约束.md'
        'Docs/工程/目录说明.md'
        'Docs/待办.md'
        'Docs/工程/注释规范.md'
        'Docs/工程/已知编辑器噪声.md'
        'Docs/配表/最新表格与SO规范.md'
    )
    '02-场景与排序层现状' = @(
        'Docs/表现/新场景接线.md'
        'Assets/Scripts/Presentation/Visual/RenderOrder.cs'
        'Assets/Scripts/Foundation/YSort.cs'
        'Assets/Scripts/Presentation/Visual/YSortAgent.cs'
        'Assets/Scripts/Data/Settings/VisualPalette.cs'
    )
    '03-既有光效通道' = @(
        'Docs/表现/粒子特效系统.md'
        'Assets/Scripts/Presentation/Effects/IEffectDriver.cs'
        'Assets/Scripts/Presentation/Effects/EffectId.cs'
        'Assets/Scripts/Presentation/Effects/EffectCatalog.cs'
        'Assets/Scripts/Presentation/Effects/Drivers/EffectDriverFactory.cs'
        'Assets/Scripts/Presentation/Effects/Drivers/ParticleDriver.cs'
        'Assets/Scripts/Presentation/Effects/Drivers/ActorTintDriver.cs'
        'Assets/Resources/materials/Shaders/SpriteFlash.shader'
        'Assets/Resources/materials/Shaders/TileFlow.shader'
    )
    '04-地块状态渲染链' = @(
        'Assets/Scripts/Presentation/Adapters/TilemapAdapter.cs'
        'Assets/Scripts/Presentation/Grid/TileObstacleDirector.cs'
        'Assets/Scripts/Presentation/Grid/TileOverlayDirector.cs'
        'Assets/Scripts/Presentation/Grid/TileHighlightView.cs'
        'Assets/Scripts/Logic/Event/Events.cs'
        'Assets/Scripts/Logic/Event/EventBus.cs'
        'Assets/Scripts/Logic/Grid/GridGeometry.cs'
        'Assets/Scripts/Logic/Grid/GridLogic.cs'
    )
    '05-组合根' = @(
        'Assets/Scripts/Presentation/Root/CombatRoot.cs'
        'Assets/Scripts/Presentation/ISceneRoot.cs'
        'Assets/Scripts/Presentation/DeepseaOil.Presentation.asmdef'
    )
}

if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
New-Item -ItemType Directory -Path $dst -Force | Out-Null

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add('全局光照规划 · 交接包清单')
$manifest.Add("打包时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$manifest.Add("来源仓库: $root")
$manifest.Add('')

$missing = New-Object System.Collections.Generic.List[string]
$clashes = New-Object System.Collections.Generic.List[string]
$seen = New-Object System.Collections.Generic.HashSet[string]
$fileCount = 0

# 白名单外的后缀补 .txt，内容不改：原格式留在文件名里，对方能自己判断怎么读。
function Get-OutName([string]$name) {
    $ext = [System.IO.Path]::GetExtension($name)
    if ($ext -and ($uploadableExt -contains $ext.ToLowerInvariant())) { return $name }
    return "$name.txt"
}

foreach ($g in $groups.Keys) {
    $manifest.Add("[$g]")

    foreach ($entry in $groups[$g]) {
        # 默认按原文件名平铺；写成 @{From=…; As=…} 可改名，避开同名覆盖
        if ($entry -is [hashtable]) { $rel = $entry.From; $asName = $entry.As } else { $rel = $entry; $asName = $null }

        $src = Join-Path $root ($rel -replace '/', '\')
        if (-not (Test-Path $src)) {
            $missing.Add($rel)
            $manifest.Add("  缺失  $rel")
            continue
        }
        $item = Get-Item $src -Force
        if ($item.PSIsContainer) {
            # 目录：整棵复制，但只取文件、全部平铺
            foreach ($f in Get-ChildItem $src -Recurse -File) {
                $outName = Get-OutName $f.Name
                if (-not $seen.Add($outName)) { $clashes.Add("$outName  ←  $rel/") }
                Copy-Item -Path $f.FullName -Destination (Join-Path $dst $outName) -Force
                $fileCount++
                $note = if ($outName -ne $f.Name) { "  (补 .txt，原 $($f.Name))" } else { '' }
                $manifest.Add("  文件  $outName  ←  $rel/$note  ($([math]::Round($f.Length / 1KB, 1)) KB)")
            }
        } else {
            $name = if ($asName) { $asName } else { $item.Name }
            $outName = Get-OutName $name
            if (-not $seen.Add($outName)) { $clashes.Add("$outName  ←  $rel") }
            Copy-Item -Path $src -Destination (Join-Path $dst $outName) -Force
            $fileCount++
            $renamed = if ($asName) { "  (改名自 $($item.Name))" } else { '' }
            $suffixed = if ($outName -ne $name) { "  (补 .txt，原 $name)" } else { '' }
            $manifest.Add("  文件  $outName  ←  $rel$renamed$suffixed  ($([math]::Round($item.Length / 1KB, 1)) KB)")
        }
    }
    $manifest.Add('')
}

# 交接说明：对方没有仓库上下文，这几条必须随包一起给（Unity 通用件已剔除，全靠这段兜）。
$manifest.Add('[随包说明]')
$manifest.Add('1. 项目：Unity 2022.3.62f3c1 + URP 14.0.12（2D Renderer）。正交相机半高 8，格子 1 世界单位。')
$manifest.Add('   主场景 Assets/Scenes/Range.unity：Grid 下六层 Tilemap（Ground 0 / InitialSetup 1 / Static 2 / Fire 3 / Water 4 / Steam 5）。')
$manifest.Add('   该场景不入 Build Settings（Begin 与 Levels/Level_1..5 才入），直接 Play 即可，是本工程唯一完整装配的场景。')
$manifest.Add('2. 四层单向依赖：Presentation → Logic → Data → Foundation（asmdef 边界，反向引用直接编译不过）。')
$manifest.Add('   光照代码只能落 Presentation：Logic 严禁 MonoBehaviour / GameObject / Transform / Component / Tilemap / Time。')
$manifest.Add('   Presentation 的 asmdef 已含 Unity.InputSystem / Cinemachine 等；URP 运行时程序集靠 autoReferenced 自动进入，')
$manifest.Add('   但要引 Unity.RenderPipelines.Universal.2D.Internal 必须在 asmdef 里显式写 references。')
$manifest.Add('3. 热路径（Tick / FixedUpdate / Update / OnBallHit / TakeDamage / 地块状态回调）0 GC：禁 LINQ、禁闭包与 lambda、')
$manifest.Add('   禁热路径 new List/Array 与 GetComponent，池化或静态委托。物理检测一律 NonAlloc。')
$manifest.Add('4. 现状（本任务的三条前提）：全工程零 Light2D 组件；TagManager 只有 Default 一个 Sorting Layer；')
$manifest.Add('   Range 六层 Tilemap 全部共用同一个默认 sprite 材质（guid a97c105638bdf8b4a8650670310a4cd3）。')
$manifest.Add('   ⇒ 逐格自发光与整层受光是两条不同成本的路，规划里必须先选边并说明理由。')
$manifest.Add('5. 上传兼容：.txt 结尾且前面还有一层扩展名的是改名件，例如 SpriteFlash.shader.txt / comment-budget.json 除外。')
$manifest.Add('   扩展名在前的那个才是真实格式；文件按字节原样，未加任何头注释，去掉末尾 .txt 即可还原。')

$manifest.Add('')
if ($missing.Count -gt 0) {
    $manifest.Add("缺失 $($missing.Count) 项：")
    foreach ($m in $missing) { $manifest.Add("  $m") }
} else {
    $manifest.Add('缺失 0 项。')
}

if ($clashes.Count -gt 0) {
    $manifest.Add("")
    $manifest.Add("同名覆盖 $($clashes.Count) 项（平铺的代价，后者覆盖前者）：")
    foreach ($c in $clashes) { $manifest.Add("  $c") }
}

$manifestPath = Join-Path $dst 'MANIFEST.txt'
[System.IO.File]::WriteAllLines($manifestPath, $manifest, (New-Object System.Text.UTF8Encoding $true))

"打包完成: $dst"
"文件总数: $fileCount"
if ($missing.Count -gt 0) { "缺失: $($missing.Count) 项（见 MANIFEST.txt）" } else { "缺失: 0" }
if ($clashes.Count -gt 0) { "同名覆盖: $($clashes.Count) 项（见 MANIFEST.txt）" } else { "同名覆盖: 0" }
