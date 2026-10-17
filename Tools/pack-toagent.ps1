# 项目交接包：把「让外部 AI 读懂本工程」所需的文档与关键源码，平铺复制到 T:\GAMES\toAgent。
# 用法：pwsh Tools/pack-toagent.ps1
# 输出：T:\GAMES\toAgent\ 下全部平铺（无子目录）＋ MANIFEST.txt
# 上传兼容：白名单外的扩展名一律在后面补 .txt（内容一字不改，原格式保留在文件名里）。
# 注意：每次运行都会先清空输出目录，再整包重建。

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dst = 'T:\GAMES\toAgent'

# 这些后缀原样上传；其余（.unity/.prefab/.asset/.mat/.shader/.asmdef/.ps1 …）补 .txt。
$uploadableExt = @('.txt', '.md', '.cs', '.json', '.xml', '.yml', '.yaml', '.csv')

# 分组 → 相对仓库根的路径。可写文件或目录（目录整棵复制，仍然平铺）。
# 写成 @{From=…; As=…} 可改名，用来避开平铺后的同名覆盖。
$groups = [ordered]@{
    '00-入口与纪律' = @(
        'AGENTS.md'
        'README.md'
        @{ From = 'Docs/README.md'; As = 'README_Docs文档地图.md' }
        @{ From = 'ConfigWorkspace/AGENTS.md'; As = 'AGENTS_配表工作区.md' }
        'Docs/工程/架构约束.md'
        'Docs/工程/目录说明.md'
        'Docs/工程/注释规范.md'
        'Docs/工程/人类接手与工程交接手册.md'
        'Docs/工程/已知编辑器噪声.md'
        'Docs/工程/加内容清单.md'
        'Docs/工程/敌人转向抖动.md'
    )
    '01-设计与现状' = @(
        'Docs/设计/策划案_荒原复苏指南_v4.0.md'
        'Docs/待办.md'
        'Docs/配表/策划配表手册.md'
        'Docs/配表/最新表格与SO规范.md'
        'Docs/美术装配指南.md'
        'Docs/工程/敌人仇恨系统_归档.md'
        'ProjectSettings/TagManager.asset'
    )
    '02-表现子系统文档' = @(
        'Docs/表现/新场景接线.md'
        'Docs/表现/全局光照.md'
        'Docs/表现/光照调参手册.md'
        'Docs/表现/粒子特效系统.md'
    )
    '03-程序集与入口' = @(
        'Assets/Scripts/Foundation/DeepseaOil.Foundation.asmdef'
        'Assets/Scripts/Data/DeepseaOil.Data.asmdef'
        'Assets/Scripts/Logic/DeepseaOil.Logic.asmdef'
        'Assets/Scripts/Presentation/DeepseaOil.Presentation.asmdef'
        'Assets/Scripts/Presentation/ISceneRoot.cs'
        'Assets/Scripts/Presentation/Root/GameRoot.cs'
        'Assets/Scripts/Presentation/Root/GameManager.cs'
        'Assets/Scripts/Presentation/Root/SceneFlowConfig.cs'
    )
    '04-组合根' = @(
        'Assets/Scripts/Presentation/Root/CombatRoot.cs'
        'Assets/Scripts/Presentation/Root/PlayerController.cs'
        'Packages/manifest.json'
    )
    '05-Logic核心' = @(
        'Assets/Scripts/Logic/LogicContext.cs'
        'Assets/Scripts/Logic/Tickable.cs'
        'Assets/Scripts/Logic/Grid/GridLogic.cs'
        'Assets/Scripts/Logic/Grid/GridGeometry.cs'
        'Assets/Scripts/Logic/Grid/ITileState.cs'
        'Assets/Scripts/Logic/Grid/TileStateMachine.cs'
        'Assets/Scripts/Logic/Grid/States/TableTileState.cs'
        'Assets/Scripts/Logic/Grid/TileChainReactor.cs'
        'Assets/Scripts/Logic/Event/Events.cs'
        'Assets/Scripts/Logic/Event/EventBus.cs'
        'Assets/Scripts/Logic/Combat/Damage.cs'
        'Assets/Scripts/Logic/Element/ReactionResolver.cs'
        'Assets/Scripts/Logic/Wave/WaveLogic.cs'
        'Assets/Scripts/Logic/Player/PlayerLogic.cs'
        'Assets/Scripts/Logic/Movement/ActorLedger.cs'
        'Assets/Scripts/Logic/Services/PauseService.cs'
    )
    '06-Data层' = @(
        'Assets/Scripts/Data/Config/ConfigModule.cs'
        'Assets/Scripts/Data/Config/TablesMeta.cs'
        'Assets/Scripts/Data/Config/StartupValidator.cs'
        'Assets/Scripts/Data/Config/Spec/PlayerSpec.cs'
        'Assets/Scripts/Data/Config/Spec/ProjectileSpec.cs'
        'Assets/Scripts/Data/Config/Spec/TileStateSpec.cs'
        'Assets/Scripts/Data/Config/Spec/EnemySpec.cs'
        'Assets/Scripts/Data/Config/Spec/DuoReactionCatalog.cs'
        'Assets/Scripts/Data/Settings/VisualPalette.cs'
        'Assets/Scripts/Data/Settings/VisualFeelTuning.cs'
        'Assets/Scripts/Data/Settings/ThrowTuning.cs'
        'Assets/Scripts/Data/Settings/EnemyTuning.cs'
        'Assets/Scripts/Data/Asset/AssetModule.cs'
        'Assets/Resources/tuning/VisualPalette.asset'
        'Assets/Resources/tuning/VisualFeelTuning.asset'
        'Assets/Resources/tuning/ThrowTuning.asset'
        'Assets/Resources/tuning/EnemyTuning.asset'
    )
    '07-表现适配与子系统' = @(
        'Assets/Scripts/Presentation/Adapters/TilemapAdapter.cs'
        'Assets/Scripts/Presentation/Adapters/ActorMotor.cs'
        'Assets/Scripts/Presentation/Adapters/GameTime.cs'
        'Assets/Scripts/Presentation/Visual/RenderOrder.cs'
        'Assets/Scripts/Presentation/Visual/SortingLayerConsts.cs'
        'Assets/Scripts/Presentation/Visual/YSortAgent.cs'
        'Assets/Scripts/Presentation/Grid/TileOverlayDirector.cs'
        'Assets/Scripts/Presentation/Grid/TileObstacleDirector.cs'
        'Assets/Scripts/Presentation/Grid/TileVfxDirector.cs'
        'Assets/Scripts/Presentation/Lighting/EnvironmentDirector.cs'
        'Assets/Scripts/Presentation/Lighting/TileGridLightmap.cs'
        'Assets/Scripts/Presentation/Lighting/TimeOfDay.cs'
        'Assets/Scripts/Presentation/Effects/EffectId.cs'
        'Assets/Scripts/Presentation/Effects/EffectCatalog.cs'
        'Assets/Scripts/Presentation/Effects/EffectModule.cs'
        'Assets/Scripts/Presentation/Effects/EffectContext.cs'
        'Assets/Scripts/Presentation/Effects/Drivers/ParticleDriver.cs'
        'Assets/Scripts/Presentation/Effects/Drivers/ActorTintDriver.cs'
        'Assets/Scripts/Presentation/World/Fountain.cs'
    )
    '08-Shader' = @(
        'Assets/Resources/materials/Shaders/CelLit.shader'
        'Assets/Resources/materials/Shaders/TileFlow.shader'
        'Assets/Resources/materials/Shaders/TilePlantFlow.shader'
        'Assets/Resources/materials/Shaders/SpriteFlash.shader'
    )
    '09-门禁与工具' = @(
        'Tools/compile-gate.ps1'
        'Tools/comment-lint.ps1'
        'Tools/comment-budget.json'
    )
    '10-测试契约' = @(
        'Assets/Tests/Runtime/Editor/光照地基Tests.cs'
        'Assets/Tests/Runtime/Editor/全局光照Tests.cs'
        'Assets/Tests/Runtime/Editor/CelLitMaterialTests.cs'
    )
}

# 输出目录只允许是这一个绝对路径：清空前挡一道，免得变量被改坏时误删别处
if (Test-Path $dst) {
    $resolved = (Resolve-Path $dst).Path.TrimEnd('\')
    if ($resolved -ne $dst.TrimEnd('\')) {
        throw "拒绝清空：解析出的路径是 $resolved，与预期的 $dst 不一致。"
    }
    Remove-Item $dst -Recurse -Force
}
New-Item -ItemType Directory -Path $dst -Force | Out-Null

$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add('项目交接包 · 给外部 AI 的文档与源码清单')
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
        if ($entry -is [hashtable]) { $rel = $entry.From; $asName = $entry.As } else { $rel = $entry; $asName = $null }

        $src = Join-Path $root ($rel -replace '/', '\')
        if (-not (Test-Path $src)) {
            $missing.Add($rel)
            $manifest.Add("  缺失  $rel")
            continue
        }
        $item = Get-Item $src -Force
        if ($item.PSIsContainer) {
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

# 交接说明：对方没有仓库上下文（打不开 Unity、看不到场景与预制体），这几条必须随包一起给。
$manifest.Add('[随包说明]')
$manifest.Add('1. 项目：Unity 2022.3.62f3c1 ＋ URP 14.0.12（2D Renderer，Linear 色彩空间），')
$manifest.Add('   Cinemachine 2.10.7、InputSystem 1.14.2、TextMeshPro 3.0.7（见 manifest.json）。正交相机半高 8，格子 1 世界单位。')
$manifest.Add('   主场景 Assets/Scenes/Range.unity 是唯一完整装配的场景（不入 Build Settings，直接 Play 即可）；')
$manifest.Add('   入 Build Settings 的是 Begin + Levels/Level_1..5。本包不含场景/预制体/材质/贴图（二进制或超长机读件）；调参 SO 例外，见第 9 条。')
$manifest.Add('2. 四层单向依赖：Presentation → Logic → Data → Foundation（asmdef 边界，反向引用直接编译不过）。')
$manifest.Add('   🔴 asmdef 的 references 不传递：用到上层公开类型时，其基类/接口所在程序集必须在本层 asmdef 里显式写全。')
$manifest.Add('   Logic 是纯 C#：严禁 MonoBehaviour / GameObject / Transform / Component / Tilemap / Time.time。')
$manifest.Add('3. 驱动模型：GameRoot.Update（渲染帧）与 GameRoot.FixedUpdate（物理帧）是唯二入口；场景系统实现 ISceneRoot 并声明 Order，')
$manifest.Add('   由 GameRoot 拓扑推进。业务组件禁止散乱 Update（现存唯一例外：EnvironmentDirector.Update 推时段过渡插值）。')
$manifest.Add('   GameRoot 是全工程唯一真单例（DontDestroyOnLoad）。')
$manifest.Add('4. 热路径（Tick / FixedTick / Update / OnBallHit / TakeDamage / 地块状态回调）0 GC：禁 LINQ、禁闭包与 lambda、')
$manifest.Add('   禁热路径 new List/Array 与 GetComponent；池化或静态委托。物理检测一律 NonAlloc。2D 排序只走 RenderOrder 频带，不靠 Z 轴。')
$manifest.Add('5. 光照现状：一盏 Global Light 2D（只照 Ground + Actor 两层，掩码经序列化字段写）＋ TileGridLightmap 网格光场')
$manifest.Add('   （一格一纹素，逐帧两趟 8 邻域扩散，结果经 4 个 shader 全局量注入）＋ CelLit / TileFlow 受光材质。')
$manifest.Add('   网格光场除火/电持续光源外还有瞬态脉冲：CombatRoot 在 GridBlastOccurred / GridPointVfxOccurred 里调 AddImpulse')
$manifest.Add('   （蒸汽取事件 Radius 作径向范围，电弧单格），脉冲先并入源头参与扩散、再按 exp(-impulseDecayRate·dt) 衰减；')
$manifest.Add('   旋钮＝[Environment] ▸ impulseDecayRate（场景现值 14）＋ VisualFeelTuning.asset 的「网格光脉冲」五字段（现值 steam 0.6 / arc 0.35）。')
$manifest.Add('   火/电判定表在 TileLightDirector.TryGetContinuousLight（旧点光池，已无驱动者，未随包）：新增发光地块状态必须在那里补 case，否则不亮且无日志。')
$manifest.Add('   Sorting Layer 共六层：Ground / Actor / Aim / Effect / UI / Default（Effect 与 UI 不受光）。')
$manifest.Add('   后处理封印在 Assets/Settings/DefaultGlobalVolumeProfile.asset；其机器判据与资产现值不一致一事记在 待办.md。')
$manifest.Add('   旧 TileLightDirector 点光池已无驱动者（仅剩静态映射表被复用），勿再挂载。')
$manifest.Add('6. 生成物禁区（每次导表会被镜像覆盖，禁止手改）：Assets/Scripts/Generated/**、Assets/StreamingAssets/Luban/**、ConfigWorkspace/output/**。')
$manifest.Add('   跑 Luban 的入口是 Unity 菜单 Luban ▸ 表格数据导入；Excel 源表在 ConfigWorkspace/Data/*.xlsx（唯一配置真源，人工维护）。')
$manifest.Add('7. 上传兼容：.txt 结尾且前面还有一层扩展名的都是改名件，例如 CelLit.shader.txt / TagManager.asset.txt / VisualFeelTuning.asset.txt / compile-gate.ps1.txt。')
$manifest.Add('   前面那层扩展名才是真实格式；文件按字节原样，未加任何头注释，去掉末尾 .txt 即可还原。comment-budget.json 是白名单内，未改名。')
$manifest.Add('8. 本包不含历史任务书（Docs/任务/**）与存档方案草稿（Docs/暂存/**）：它们含已被推翻的数值与做法，只作变更史，读了会被误导。')
$manifest.Add('   当前事实以本包内的 工程/表现 文档 + 源码为准；文档与代码冲突时信代码。')
$manifest.Add('9. 调参现值：随包 4 份 Assets/Resources/tuning/*.asset（VisualPalette / VisualFeelTuning / ThrowTuning / EnemyTuning）')
$manifest.Add('   是编辑器里的真实现值，与同名 .cs 的 C# 默认值并不相同（例：steamImpulseIntensity 代码默认 1.8、资产现值 0.6）；冲突时以 .asset 为准。')
$manifest.Add('   场景里的手调值不随包（Range.unity 775 KB），只能按字段名比对；场景网格光场现存值：airTransmission 0.75、obstacleTransmission 0.2、impulseDecayRate 14。')
$manifest.Add('10. 已知偏差（文档落后于代码，别把文档当全量事实）：光照调参手册.md 尚未收录网格光场的瞬态脉冲旋钮')
$manifest.Add('    （impulseDecayRate 与 VisualFeelTuning 的 steamImpulse* / arcImpulse* 五字段）；该子系统目前只有 TileGridLightmap.cs /')
$manifest.Add('    CombatRoot.cs / VisualFeelTuning.cs 的 XML 注释可读。待办.md 已登记此缺口与 TileGridLightmapTests 的失效断言。')
$manifest.Add('')
$manifest.Add('[建议阅读顺序]')
$manifest.Add('  ① AGENTS.md（最高行为宪章）→ 架构约束.md → 目录说明.md → 人类接手与工程交接手册.md')
$manifest.Add('  ② 全局光照.md / 粒子特效系统.md / 新场景接线.md（三个表现子系统怎么跑起来；瞬态脉冲以代码为准，见第 10 条）')
$manifest.Add('  ③ 策划案_荒原复苏指南_v4.0.md + 最新表格与SO规范.md（设计意图与"表 vs SO"权责边界）')
$manifest.Add('  ④ 代码按数据流逆流而下：GameRoot → CombatRoot → TilemapAdapter / TileGridLightmap → GridLogic / EventBus → Spec / 调参SO')
$manifest.Add('  ⑤ 待办.md 收尾（已知缺陷、未收口项、编辑器内待人工验收清单）')
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
