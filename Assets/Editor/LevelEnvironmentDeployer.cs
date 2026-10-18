#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DeepseaOil.Presentation;
using DeepseaOil.Presentation.Combat;
using DeepseaOil.Presentation.Grid;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using GridComponent = UnityEngine.Grid;

namespace DeepseaOil.EditorTools
{
    /// <summary>关卡环境预置体打包与批量幂等部署</summary>
    /// <remarks>预置体只含 [Environment] 与 TileCursorIndicator；残血 UI 由常驻 GamePlayPanel 提供，禁止逐关复制。</remarks>
    public sealed class LevelEnvironmentDeployer : EditorWindow
    {
        private const string RangeScenePath = "Assets/Scenes/Range.unity";
        private const string DefaultLevelSceneFolder = "Assets/Scenes/Levels";
        private const string PresetPrefabFolder = "Assets/Prefabs/Environment";
        private const string PresetPrefabPath = "Assets/Prefabs/Environment/[LevelEnvironment_Preset].prefab";
        private const string PresetRootName = "[LevelEnvironment_Preset]";
        private const string EnvironmentRootName = "[Environment]";
        private const string CursorPrefabPath = "Assets/Resources/scane_prebs/TileCursorIndicator.prefab";
        private const string WeaponPrefabPath = "Assets/Resources/scane_prebs/Weapon.prefab";
        private const string WeaponRootName = "Weapon";
        private const string GamePlayPanelPath = "Assets/Resources/ui/Panel/GamePlayPanel.prefab";

        private GameObject _presetPrefab;
        private DefaultAsset _targetScenesFolder;
        private bool _isDryRun = true;
        private bool _confirmGitClean;
        private bool _isRunning;
        private Vector2 _scrollPos;

        [MenuItem("Tools/关卡部署流水线/场景环境批量适配工具", false, 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<LevelEnvironmentDeployer>("环境批量适配工具");
            window.minSize = new Vector2(480, 560);
            window.Show();
        }

        private void OnEnable()
        {
            _presetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresetPrefabPath);
            _targetScenesFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultLevelSceneFolder);
            _isDryRun = true;
            _confirmGitClean = false;
            _isRunning = false;
        }

        private void OnFocus()
        {
            if (_presetPrefab == null)
            {
                _presetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresetPrefabPath);
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("关卡环境工业化部署流水线", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "阶段一：在 Range.unity 中打包 [LevelEnvironment_Preset]。\n" +
                "阶段二：对 Level 场景做 Dry-Run 或幂等部署。残血 UI 属于常驻 GamePlayPanel，不进入每关预置体。",
                MessageType.Info);

            bool busy = _isRunning || IsEditorBusy();

            EditorGUILayout.Space(10);
            DrawSectionPresetBaking(busy);

            EditorGUILayout.Space(15);
            DrawSectionBatchDeploy(busy);

            EditorGUILayout.EndScrollView();
        }

        private void DrawSectionPresetBaking(bool busy)
        {
            EditorGUILayout.LabelField("阶段一：基准环境预制体打包", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _presetPrefab = (GameObject)EditorGUILayout.ObjectField(
                "当前环境预制体", _presetPrefab, typeof(GameObject), false);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("当前打开场景:", activeScene.name);

            bool isRange = string.Equals(activeScene.path, RangeScenePath, StringComparison.OrdinalIgnoreCase);

            if (!isRange)
            {
                EditorGUILayout.HelpBox("只有基准场景 Range.unity 允许执行打包。", MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(busy || !isRange);

            if (GUILayout.Button("从当前场景提取并生成 [LevelEnvironment_Preset] 预制体", GUILayout.Height(32)))
            {
                BakePresetFromActiveScene();
            }

            EditorGUI.EndDisabledGroup();

            if (_presetPrefab == null)
            {
                EditorGUILayout.HelpBox("尚未生成预置体，或资产路径与默认路径不一致。", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSectionBatchDeploy(bool busy)
        {
            EditorGUILayout.LabelField("阶段二：关卡场景批量幂等部署", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _targetScenesFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "关卡场景所在目录", _targetScenesFolder, typeof(DefaultAsset), false);

            _isDryRun = EditorGUILayout.ToggleLeft(
                "安全模式 (Dry-Run: 仅分析打印日志，不修改不保存任何场景)",
                _isDryRun,
                EditorStyles.boldLabel);

            if (_isDryRun)
            {
                EditorGUILayout.HelpBox(
                    "当前处于 Dry-Run：工具只打开场景做静态分析，不 MarkDirty、不保存、不实例化。",
                    MessageType.Warning);
            }
            else
            {
                _confirmGitClean = EditorGUILayout.ToggleLeft(
                    "我确认 Git 工作区洁净且已备份",
                    _confirmGitClean,
                    EditorStyles.boldLabel);

                EditorGUILayout.HelpBox(
                    "【危险操作】已关闭 Dry-Run：执行将直接修改并覆写目标场景文件！",
                    MessageType.Error);

                if (!_confirmGitClean)
                {
                    EditorGUILayout.HelpBox("未确认 Git 状态，正式执行按钮保持禁用。", MessageType.Warning);
                }
            }

            EditorGUILayout.HelpBox(
                "部署会同时补齐场景根 Weapon（引用由 WeaponAimController 自愈），并把 [LevelEnvironment_Preset] 移到首位。",
                MessageType.None);

            EditorGUILayout.Space(6);

            bool canRun = !busy
                          && _presetPrefab != null
                          && (_isDryRun || _confirmGitClean)
                          && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_presetPrefab));

            EditorGUI.BeginDisabledGroup(!canRun);

            if (GUILayout.Button(
                    _isDryRun
                        ? "① 模拟试运行 (Dry-Run 选中的 Project 场景)"
                        : "② 实际执行批量部署 (选中的 Project 场景)",
                    GUILayout.Height(34)))
            {
                DeployToSelectedScenesInProject(_isDryRun);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!canRun || _targetScenesFolder == null);

            if (GUILayout.Button(
                    _isDryRun
                        ? "① 模拟试运行 (Dry-Run 目标目录下全部 Level 场景)"
                        : "② 实际执行批量部署 (目标目录下全部 Level 场景)",
                    GUILayout.Height(30)))
            {
                DeployToFolderScenes(_targetScenesFolder, _isDryRun);
            }

            EditorGUI.EndDisabledGroup();

            if (_presetPrefab == null)
            {
                EditorGUILayout.HelpBox("请先在阶段一生成预置体，或手动指定已存在的 [LevelEnvironment_Preset].prefab。", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void BakePresetFromActiveScene()
        {
            if (_isRunning || IsEditorBusy()) return;

            Scene activeScene = SceneManager.GetActiveScene();

            if (!string.Equals(activeScene.path, RangeScenePath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("场景错误", "打包必须在 Range.unity 中执行。", "确定");
                return;
            }

            List<string> issues = new List<string>();

            if (FindSceneObjectsByName(activeScene, PresetRootName).Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "已存在预置体节点",
                    "Range 中已经存在 [LevelEnvironment_Preset]。请先删除旧节点，工具不会生成第二个副本。",
                    "确定");
                return;
            }

            GameObject environmentRoot = FindSingleEnvironmentRoot(activeScene, issues);
            TileCursorIndicator cursor = FindSingleCursor(activeScene, issues);

            if (!ValidateEnvironmentSource(environmentRoot, cursor, issues))
            {
                LogIssues("打包前校验失败", issues);
                EditorUtility.DisplayDialog("打包中止", "环境源校验失败，详见 Console。", "确定");
                return;
            }

            if (!ValidateGlobalLowHealthUi())
            {
                Debug.LogWarning("[Bake] 未在 GamePlayPanel 中找到 LowHealthScreenEffect；残血 UI 不进入预置体。");
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PresetPrefabPath);

            if (existingPrefab != null
                && !EditorUtility.DisplayDialog(
                    "覆盖预置体",
                    "已存在 [LevelEnvironment_Preset].prefab，确定覆盖吗？",
                    "覆盖",
                    "取消"))
            {
                return;
            }

            GameObject cursorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CursorPrefabPath);

            if (cursorPrefab == null)
            {
                Debug.LogError("[Bake] 找不到：" + CursorPrefabPath);
                EditorUtility.DisplayDialog("打包中止", "找不到 TileCursorIndicator 预制体。", "确定");
                return;
            }

            _isRunning = true;
            GameObject tempRoot = null;

            try
            {
                EnsureFolder(PresetPrefabFolder);

                tempRoot = new GameObject(PresetRootName);
                ResetTransform(tempRoot.transform);

                GameObject environmentClone = (GameObject)UnityEngine.Object.Instantiate(
                    environmentRoot, tempRoot.transform);
                environmentClone.name = EnvironmentRootName;
                environmentClone.SetActive(true);
                ResetTransform(environmentClone.transform);

                EnvironmentDirector clonedDirector =
                    environmentClone.GetComponentInChildren<EnvironmentDirector>(true);
                Light2D clonedGlobalLight = environmentClone.GetComponentInChildren<Light2D>(true);

                if (clonedDirector != null && clonedGlobalLight != null)
                {
                    SerializedObject serializedDirector = new SerializedObject(clonedDirector);
                    SerializedProperty globalLightProperty =
                        serializedDirector.FindProperty("globalLight");

                    if (globalLightProperty != null)
                    {
                        globalLightProperty.objectReferenceValue = clonedGlobalLight;
                        serializedDirector.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                GameObject cursorInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                    cursorPrefab, tempRoot.transform);

                if (cursorInstance == null)
                {
                    Debug.LogError("[Bake] TileCursorIndicator 实例化失败。");
                    EditorUtility.DisplayDialog("打包中止", "TileCursorIndicator 实例化失败。", "确定");
                    return;
                }

                cursorInstance.name = "TileCursorIndicator";
                cursorInstance.SetActive(true);
                ResetTransform(cursorInstance.transform);

                List<string> presetIssues = new List<string>();

                if (!ValidatePreset(tempRoot, presetIssues))
                {
                    LogIssues("预置体校验失败", presetIssues);
                    EditorUtility.DisplayDialog("打包中止", "生成的预置体未通过校验，详见 Console。", "确定");
                    return;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempRoot, PresetPrefabPath);

                if (savedPrefab == null)
                {
                    Debug.LogError("[Bake] Prefab 保存失败：" + PresetPrefabPath);
                    EditorUtility.DisplayDialog("打包中止", "Prefab 保存失败。", "确定");
                    return;
                }

                AssetDatabase.SaveAssets();
                _presetPrefab = savedPrefab;
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);

                Debug.Log(
                    "[Bake] 已生成 " + PresetPrefabPath +
                    "：EnvironmentDirector=1、Global Light2D=1、Volume=1、TileGridLightmap=1、TileCursorIndicator=1；" +
                    "不含 Canvas/Camera/Grid/LowHealthScreenEffect/WeaponAimController。");

                EditorUtility.DisplayDialog(
                    "打包成功",
                    "已生成：\n" + PresetPrefabPath +
                    "\n\n临时节点已销毁；Range 层级未被改写。",
                    "好的");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Bake] 打包异常：" + ex);
                EditorUtility.DisplayDialog("打包异常", ex.Message, "确定");
            }
            finally
            {
                if (tempRoot != null)
                {
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(tempRoot);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Bake] 临时节点清理失败：" + ex.Message);
                    }
                }

                _isRunning = false;
                Repaint();
            }
        }

        private void DeployToSelectedScenesInProject(bool dryRun)
        {
            if (!CanStart()) return;

            List<string> scenePaths = new List<string>();
            UnityEngine.Object[] selected = Selection.objects;

            for (int i = 0; i < selected.Length; i++)
            {
                UnityEngine.Object item = selected[i];

                if (item == null) continue;

                string path = AssetDatabase.GetAssetPath(item);

                if (string.IsNullOrEmpty(path)) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    CollectFolderScenes(path, scenePaths);
                }
                else if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenePath(path, scenePaths);
                }
            }

            scenePaths = NormalizeScenePaths(scenePaths);

            if (scenePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请选中一个或多个 .unity 场景，或选中包含关卡场景的文件夹。", "确定");
                return;
            }

            ExecuteBatchDeploy(scenePaths, dryRun);
        }

        private void DeployToFolderScenes(DefaultAsset folderAsset, bool dryRun)
        {
            if (!CanStart() || folderAsset == null) return;

            string folderPath = AssetDatabase.GetAssetPath(folderAsset);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("提示", "指定对象不是有效文件夹。", "确定");
                return;
            }

            List<string> scenePaths = new List<string>();
            CollectFolderScenes(folderPath, scenePaths);
            scenePaths = NormalizeScenePaths(scenePaths);

            if (scenePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "目录下没有 Level_*.unity 场景。", "确定");
                return;
            }

            ExecuteBatchDeploy(scenePaths, dryRun);
        }

        private void ExecuteBatchDeploy(List<string> scenePaths, bool dryRun)
        {
            if (!dryRun && !_confirmGitClean)
            {
                Debug.LogError("[Deploy] 未确认 Git 工作区洁净，已中止。");
                return;
            }

            List<string> presetIssues = new List<string>();

            if (!ValidatePreset(_presetPrefab, presetIssues))
            {
                LogIssues("预置体校验失败", presetIssues);
                EditorUtility.DisplayDialog("预置体无效", "目标预置体未通过校验，详见 Console。", "确定");
                return;
            }

            GameObject presetPrefab = _presetPrefab;
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
            List<string> weaponIssues = new List<string>();

            if (!ValidateWeaponPrefab(weaponPrefab, weaponIssues))
            {
                LogIssues("Weapon 预置体校验失败", weaponIssues);
                EditorUtility.DisplayDialog("Weapon 无效", "目标 Weapon 预置体未通过校验，详见 Console。", "确定");
                return;
            }

            if (!dryRun)
            {
                if (!EditorUtility.DisplayDialog(
                        "正式执行警告",
                        "即将对 " + scenePaths.Count +
                        " 个场景执行真实修改与保存。\n\n请确保 Git 工作区洁净且已备份。\n确定继续吗？",
                        "立即执行",
                        "取消"))
                {
                    return;
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[Deploy] 用户取消了未保存场景的存储，部署中断。");
                    return;
                }
            }

            _isRunning = true;

            string originalScenePath = SceneManager.GetActiveScene().path;
            int processed = 0;
            int plannedInject = 0;
            int plannedWeapon = 0;
            int plannedUpdate = 0;
            int injected = 0;
            int weaponInjected = 0;
            int updated = 0;
            int skipped = 0;
            int blocked = 0;
            int failed = 0;
            bool cancelled = false;

            Debug.Log("<color=#00FFCC>==================== 开始批量关卡部署 (DryRun=" + dryRun + ") ====================</color>");

            try
            {
                if (!ValidateGlobalLowHealthUi())
                {
                    Debug.LogWarning("[Deploy] 未找到常驻 LowHealthScreenEffect；残血全屏不会随预置体部署。");
                }

                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string path = scenePaths[i];
                    string sceneName = Path.GetFileNameWithoutExtension(path);

                    if (!IsInBuildSettings(path))
                    {
                        Debug.LogWarning("[Deploy] " + sceneName + " 未加入 Build Settings。");
                    }

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "批量场景部署中...",
                            "处理场景: " + sceneName + " (" + (i + 1) + "/" + scenePaths.Count + ")",
                            (float)i / scenePaths.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    Scene scene = default;
                    bool openedByTool = false;

                    try
                    {
                        scene = SceneManager.GetSceneByPath(path);

                        if (!scene.IsValid() || !scene.isLoaded)
                        {
                            scene = EditorSceneManager.OpenScene(
                                path,
                                dryRun ? OpenSceneMode.Additive : OpenSceneMode.Single);
                            openedByTool = true;
                        }

                        ScenePlan plan = AnalyzeScene(scene, presetPrefab, weaponPrefab);
                        processed++;

                        if (plan.IsBlocked)
                        {
                            blocked++;
                            Debug.LogError(
                                "<color=#FF5555>[阻断]</color> " + sceneName + " | " + FormatIssues(plan.Issues));
                            continue;
                        }

                        if (dryRun)
                        {
                            if (plan.CanInject)
                            {
                                plannedInject++;
                            }

                            if (plan.CanInjectWeapon)
                            {
                                plannedWeapon++;
                            }

                            if (plan.CanUpdateCamera || plan.NeedsOrderFix)
                            {
                                plannedUpdate++;
                            }

                            if (!plan.CanInject && !plan.CanInjectWeapon
                                && !plan.CanUpdateCamera && !plan.NeedsOrderFix)
                            {
                                skipped++;
                            }

                            LogPlan(sceneName, plan, true);
                            continue;
                        }

                        bool changed = false;
                        bool injectedEnvironment = false;
                        bool injectedWeapon = false;
                        bool orderFixed = false;
                        bool cameraChanged = false;
                        string cameraWarning = string.Empty;

                        if (plan.CanInject)
                        {
                            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(presetPrefab, scene);

                            if (instance == null)
                            {
                                failed++;
                                Debug.LogError("[Deploy] " + sceneName + " 实例化环境预置体失败。");
                                break;
                            }

                            instance.name = PresetRootName;
                            instance.SetActive(true);
                            ResetTransform(instance.transform);
                            instance.transform.SetAsFirstSibling();
                            injectedEnvironment = true;
                            changed = true;
                        }
                        else if (plan.NeedsOrderFix && plan.PresetRoot != null)
                        {
                            plan.PresetRoot.transform.SetAsFirstSibling();
                            orderFixed = true;
                            changed = true;
                        }

                        if (plan.CanInjectWeapon)
                        {
                            GameObject weaponInstance = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, scene);

                            if (weaponInstance == null)
                            {
                                failed++;
                                Debug.LogError("[Deploy] " + sceneName + " 实例化 Weapon 失败。");
                                break;
                            }

                            weaponInstance.name = WeaponRootName;
                            weaponInstance.SetActive(true);
                            weaponInstance.transform.position = Vector3.zero;
                            weaponInstance.transform.rotation = Quaternion.identity;
                            weaponInstance.transform.SetAsLastSibling();
                            injectedWeapon = true;
                            changed = true;
                        }

                        if (plan.CanUpdateCamera || plan.CameraNeedsChange)
                        {
                            if (!EnsureCameraPostProcessing(plan.Camera, out cameraChanged, out cameraWarning))
                            {
                                failed++;
                                Debug.LogError("[Deploy] " + sceneName + " 相机后处理开启失败。");
                                break;
                            }

                            if (!string.IsNullOrEmpty(cameraWarning))
                            {
                                Debug.LogWarning("[Deploy] " + sceneName + " | " + cameraWarning);
                            }

                            if (cameraChanged)
                            {
                                changed = true;
                            }
                        }

                        if (!changed)
                        {
                            skipped++;
                            Debug.Log("<color=#FFAA00>[已存在跳过]</color> " + sceneName + "：环境、Weapon、根顺序与相机后处理均已就绪。");
                            continue;
                        }

                        EditorSceneManager.MarkSceneDirty(scene);

                        if (!EditorSceneManager.SaveScene(scene))
                        {
                            failed++;
                            Debug.LogError("[Deploy] " + sceneName + " 保存失败。");
                            break;
                        }

                        if (injectedEnvironment) injected++;
                        if (injectedWeapon) weaponInjected++;
                        if (orderFixed || cameraChanged) updated++;

                        string detail = string.Empty;

                        if (injectedEnvironment) detail += "环境预置体";
                        if (injectedWeapon) detail += (detail.Length > 0 ? " + " : string.Empty) + "Weapon";
                        if (orderFixed) detail += (detail.Length > 0 ? " + " : string.Empty) + "环境根置顶";
                        if (cameraChanged) detail += (detail.Length > 0 ? " + " : string.Empty) + "相机后处理";

                        Debug.Log("<color=#00FF88>[注入/更新并保存]</color> " + sceneName + "：" + detail + "。");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Debug.LogError("[Deploy] " + sceneName + " 处理异常：" + ex);

                        if (!dryRun) break;
                    }
                    finally
                    {
                        if (openedByTool && dryRun && scene.IsValid() && scene.isLoaded)
                        {
                            try
                            {
                                EditorSceneManager.CloseScene(scene, true);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[Deploy] 关闭临时场景失败：" + ex.Message);
                            }
                        }

                        EditorUtility.UnloadUnusedAssetsImmediate();
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!dryRun)
                {
                    try
                    {
                        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[Deploy] 收尾保存提示异常：" + ex.Message);
                    }

                    RestoreOriginalScene(originalScenePath);
                }

                EditorUtility.UnloadUnusedAssetsImmediate();
                _isRunning = false;
                Repaint();
            }

            Debug.Log(
                "<color=#00FFCC>==================== 部署结束 总结报告 ====================</color>\n" +
                "处理总数: " + processed +
                " | 拟注入环境: " + plannedInject +
                " | 拟注入 Weapon: " + plannedWeapon +
                " | 拟更新/置顶: " + plannedUpdate +
                " | 实际注入环境: " + injected +
                " | 实际注入 Weapon: " + weaponInjected +
                " | 实际更新/置顶: " + updated +
                " | 已存在跳过: " + skipped +
                " | 阻断: " + blocked +
                " | 失败: " + failed +
                " | 取消: " + cancelled +
                " | 模式: " + (dryRun ? "Dry-Run(安全预览)" : "正式覆写"));
        }

        private static ScenePlan AnalyzeScene(Scene scene, GameObject presetPrefab, GameObject weaponPrefab)
        {
            ScenePlan plan = new ScenePlan();
            plan.Path = scene.path;

            List<GameObject> presetRoots = FindSceneObjectsByName(scene, PresetRootName);

            if (presetRoots.Count > 1)
            {
                plan.Issues.Add("存在 " + presetRoots.Count + " 个 [LevelEnvironment_Preset]，疑似重复注入");
                plan.IsBlocked = true;
                return plan;
            }

            GameObject presetRoot = presetRoots.Count == 1 ? presetRoots[0] : null;
            plan.PresetRoot = presetRoot;
            plan.NeedsOrderFix = presetRoot != null && presetRoot.transform.GetSiblingIndex() != 0;
            Camera camera = FindMainCamera(scene);
            plan.Camera = camera;

            if (camera == null)
            {
                plan.Issues.Add("缺少 Tag=MainCamera 的相机");
            }
            else
            {
                if (!HasCinemachineBrain(camera))
                {
                    plan.Warnings.Add("Main Camera 未挂 CinemachineBrain，残血镜头心跳不会触发");
                }

                bool cameraChanged;
                string cameraWarning;

                if (!EvaluateCameraPostProcessing(camera, true, out cameraChanged, out cameraWarning))
                {
                    plan.Issues.Add(cameraWarning);
                }
                else
                {
                    plan.CameraNeedsChange = cameraChanged;

                    if (!string.IsNullOrEmpty(cameraWarning))
                    {
                        plan.Warnings.Add(cameraWarning);
                    }
                }
            }

            if (!HasGrid(scene))
            {
                plan.Issues.Add("场景缺少 UnityEngine.Grid");
            }

            if (presetRoot != null)
            {
                if (presetRoot.transform.parent != null)
                {
                    plan.Issues.Add("[LevelEnvironment_Preset] 必须位于场景根，不能嵌套");
                }

                if (!IsPrefabInstanceOf(presetRoot, presetPrefab))
                {
                    plan.Issues.Add("[LevelEnvironment_Preset] 不是目标 Prefab 实例");
                }

                List<string> presetIssues = new List<string>();

                if (!ValidatePreset(presetRoot, presetIssues))
                {
                    plan.Issues.AddRange(presetIssues);
                }
            }

            int conflicts = CountConflicts(scene, presetRoot != null ? presetRoot.transform : null);

            if (conflicts > 0)
            {
                plan.Issues.Add(
                    "预置体外存在 " + conflicts +
                    " 个散落环境组件（Global Light2D / Volume / EnvironmentDirector / TileGridLightmap / TileCursorIndicator）");
            }

            List<WeaponAimController> weapons = FindComponentsInScene<WeaponAimController>(scene);

            if (weapons.Count > 1)
            {
                plan.Issues.Add("场景中存在 " + weapons.Count + " 个 WeaponAimController，疑似重复 Weapon");
            }
            else if (weapons.Count == 1)
            {
                WeaponAimController weapon = weapons[0];

                if (!IsPrefabInstanceOf(weapon.gameObject, weaponPrefab))
                {
                    plan.Issues.Add("场景中的 Weapon 不是目标 Weapon.prefab 实例");
                }

                if (weapon.transform.parent != null)
                {
                    plan.Issues.Add("Weapon 必须位于场景根，不能挂在 Player 下");
                }

                if (!weapon.enabled)
                {
                    plan.Issues.Add("WeaponAimController 未启用");
                }

                if (!weapon.gameObject.activeSelf)
                {
                    plan.Issues.Add("Weapon 根未激活");
                }
            }
            else
            {
                if (FindSceneObjectsByName(scene, WeaponRootName).Count > 0)
                {
                    plan.Issues.Add("场景存在名为 Weapon 的节点，但缺少 WeaponAimController");
                }
                else
                {
                    plan.CanInjectWeapon = true;
                }
            }

            if (FindComponentsInScene<PlayerController>(scene).Count == 0)
            {
                plan.Issues.Add("缺少 PlayerController，Weapon 无法自愈接线");
            }

            if (FindComponentsInScene<InputProvider>(scene).Count == 0)
            {
                plan.Issues.Add("缺少 InputProvider，Weapon 无法自愈接线");
            }

            if (plan.Issues.Count > 0)
            {
                plan.IsBlocked = true;
                return plan;
            }

            if (presetRoot == null)
            {
                plan.CanInject = true;
            }
            else
            {
                if (plan.CameraNeedsChange)
                {
                    plan.CanUpdateCamera = true;
                }

                if (!plan.NeedsOrderFix && !plan.CanUpdateCamera && !plan.CanInjectWeapon)
                {
                    plan.IsSkip = true;
                }
            }

            return plan;
        }

        private static void LogPlan(string sceneName, ScenePlan plan, bool dryRun)
        {
            if (!dryRun) return;

            string action;

            List<string> actions = new List<string>();

            if (plan.CanInject)
            {
                actions.Add("拟注入 [LevelEnvironment_Preset]");
            }

            if (plan.CanInjectWeapon)
            {
                actions.Add("拟注入 Weapon（自愈接线）");
            }

            if (plan.NeedsOrderFix)
            {
                actions.Add("拟置顶环境根");
            }

            if (plan.CanUpdateCamera || (plan.CanInject && plan.CameraNeedsChange))
            {
                actions.Add("开启 Main Camera 后处理");
            }

            action = actions.Count > 0 ? string.Join("，", actions.ToArray()) : "已存在跳过";

            string warnings = plan.Warnings.Count > 0 ? " | 警告: " + FormatIssues(plan.Warnings) : string.Empty;

            Debug.Log("<color=#00FFCC>[Dry-Run]</color> " + sceneName + " | " + action + warnings);
        }

        private static bool EvaluateCameraPostProcessing(
            Camera camera,
            bool dryRun,
            out bool changed,
            out string warning)
        {
            changed = false;
            warning = string.Empty;

            if (camera == null)
            {
                warning = "缺少 MainCamera，无法开启后处理";
                return false;
            }

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null)
            {
                if (dryRun)
                {
                    changed = true;
                    return true;
                }

                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                changed = true;
            }

            if (!cameraData.renderPostProcessing)
            {
                if (dryRun)
                {
                    changed = true;
                }
                else
                {
                    cameraData.renderPostProcessing = true;
                    EditorUtility.SetDirty(cameraData);
                    changed = true;
                }
            }

            if (cameraData.volumeLayerMask.value == 0)
            {
                warning = "Volume Layer Mask 为 0，Global Volume 不会生效";
            }

            return true;
        }

        private static bool EnsureCameraPostProcessing(
            Camera camera,
            out bool changed,
            out string warning)
        {
            return EvaluateCameraPostProcessing(camera, false, out changed, out warning);
        }

        private static Camera FindMainCamera(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);

                for (int j = 0; j < cameras.Length; j++)
                {
                    if (cameras[j] != null && cameras[j].CompareTag("MainCamera"))
                    {
                        return cameras[j];
                    }
                }
            }

            return null;
        }

        private static bool HasGrid(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<GridComponent>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCinemachineBrain(Camera camera)
        {
            return camera != null && camera.GetComponent("CinemachineBrain") != null;
        }

        private static GameObject FindSingleEnvironmentRoot(Scene scene, List<string> issues)
        {
            List<GameObject> roots = FindSceneObjectsByName(scene, EnvironmentRootName);

            if (roots.Count != 1)
            {
                issues.Add("Range 中应恰好有 1 个 " + EnvironmentRootName + "，实际 " + roots.Count + " 个");
                return null;
            }

            return roots[0];
        }

        private static TileCursorIndicator FindSingleCursor(Scene scene, List<string> issues)
        {
            List<TileCursorIndicator> cursors = new List<TileCursorIndicator>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                cursors.AddRange(roots[i].GetComponentsInChildren<TileCursorIndicator>(true));
            }

            if (cursors.Count != 1)
            {
                issues.Add("场景中应恰好有 1 个 TileCursorIndicator，实际 " + cursors.Count + " 个");
                return null;
            }

            return cursors[0];
        }

        private static bool ValidateEnvironmentSource(
            GameObject environmentRoot,
            TileCursorIndicator cursor,
            List<string> issues)
        {
            int before = issues.Count;

            if (environmentRoot == null)
            {
                issues.Add("环境源为空");
                return false;
            }

            if (!environmentRoot.activeSelf)
            {
                issues.Add(EnvironmentRootName + " 未激活");
            }

            EnvironmentDirector[] directors =
                environmentRoot.GetComponentsInChildren<EnvironmentDirector>(true);

            if (directors.Length != 1)
            {
                issues.Add("[Environment] 应恰好有 1 个 EnvironmentDirector，实际 " + directors.Length + " 个");
            }
            else if (!directors[0].enabled)
            {
                issues.Add("EnvironmentDirector 未启用");
            }

            TileGridLightmap[] lightmaps =
                environmentRoot.GetComponentsInChildren<TileGridLightmap>(true);

            if (lightmaps.Length != 1)
            {
                issues.Add("[Environment] 应恰好有 1 个 TileGridLightmap，实际 " + lightmaps.Length + " 个");
            }
            else if (!lightmaps[0].enabled)
            {
                issues.Add("TileGridLightmap 未启用");
            }

            Light2D[] lights = environmentRoot.GetComponentsInChildren<Light2D>(true);
            int globalLights = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Global) globalLights++;
            }

            if (lights.Length != 1 || globalLights != 1)
            {
                issues.Add(
                    "[Environment] 应恰好有 1 盏 Global Light2D，实际 Light2D " + lights.Length +
                    " 盏（Global " + globalLights + " 盏）");
            }
            else if (!lights[0].enabled)
            {
                issues.Add("Global Light2D 未启用");
            }

            Volume[] volumes = environmentRoot.GetComponentsInChildren<Volume>(true);

            if (volumes.Length != 1)
            {
                issues.Add("[Environment] 应恰好有 1 个 Volume，实际 " + volumes.Length + " 个");
            }
            else
            {
                if (!volumes[0].isGlobal)
                {
                    issues.Add("[Environment] 的 Volume 不是 Global");
                }

                if (!volumes[0].enabled)
                {
                    issues.Add("[Environment] 的 Volume 未启用");
                }

                if (volumes[0].sharedProfile == null)
                {
                    issues.Add("[Environment] 的 Volume 未绑定 Profile");
                }
            }

            if (directors.Length == 1)
            {
                SerializedObject serialized = new SerializedObject(directors[0]);
                SerializedProperty globalLight = serialized.FindProperty("globalLight");

                if (globalLight == null || globalLight.objectReferenceValue == null)
                {
                    issues.Add("EnvironmentDirector.globalLight 未接线");
                }
                else
                {
                    Component lightComponent = globalLight.objectReferenceValue as Component;

                    if (lightComponent == null
                        || !(lightComponent.transform == environmentRoot.transform
                             || lightComponent.transform.IsChildOf(environmentRoot.transform)))
                    {
                        issues.Add("EnvironmentDirector.globalLight 指向 [Environment] 外部");
                    }
                }
            }

            if (cursor == null)
            {
                issues.Add("TileCursorIndicator 为空");
            }
            else
            {
                if (!cursor.enabled)
                {
                    issues.Add("TileCursorIndicator 未启用");
                }

                if (cursor.transform == environmentRoot.transform
                    || cursor.transform.IsChildOf(environmentRoot.transform))
                {
                    issues.Add("TileCursorIndicator 必须位于 [Environment] 之外，才能以 Prefab 链接嵌入预置体");
                }

                SerializedObject serialized = new SerializedObject(cursor);
                SerializedProperty targetGrid = serialized.FindProperty("targetGrid");

                if (targetGrid != null && targetGrid.objectReferenceValue != null)
                {
                    issues.Add("TileCursorIndicator.targetGrid 必须为空，运行时才能自动寻址场景 Grid");
                }
            }

            return issues.Count == before;
        }

        private static bool ValidatePreset(GameObject root, List<string> issues)
        {
            int before = issues.Count;

            if (root == null)
            {
                issues.Add("预置体为空");
                return false;
            }

            if (!root.activeSelf)
            {
                issues.Add("预置体根未激活");
            }

            if (!string.Equals(root.name, PresetRootName, StringComparison.Ordinal))
            {
                issues.Add("预置体根名应为 " + PresetRootName + "，实际 " + root.name);
            }

            EnvironmentDirector[] directors = root.GetComponentsInChildren<EnvironmentDirector>(true);

            if (directors.Length != 1)
            {
                issues.Add("预置体应恰好有 1 个 EnvironmentDirector，实际 " + directors.Length + " 个");
            }
            else if (!directors[0].enabled)
            {
                issues.Add("预置体的 EnvironmentDirector 未启用");
            }

            TileGridLightmap[] lightmaps = root.GetComponentsInChildren<TileGridLightmap>(true);

            if (lightmaps.Length != 1)
            {
                issues.Add("预置体应恰好有 1 个 TileGridLightmap，实际 " + lightmaps.Length + " 个");
            }
            else if (!lightmaps[0].enabled)
            {
                issues.Add("预置体的 TileGridLightmap 未启用");
            }

            TileCursorIndicator[] cursors = root.GetComponentsInChildren<TileCursorIndicator>(true);

            if (cursors.Length != 1)
            {
                issues.Add("预置体应恰好有 1 个 TileCursorIndicator，实际 " + cursors.Length + " 个");
            }
            else if (!cursors[0].enabled)
            {
                issues.Add("预置体的 TileCursorIndicator 未启用");
            }

            Light2D[] lights = root.GetComponentsInChildren<Light2D>(true);
            int globalLights = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Global) globalLights++;
            }

            if (lights.Length != 1 || globalLights != 1)
            {
                issues.Add(
                    "预置体应恰好有 1 盏 Global Light2D，实际 Light2D " + lights.Length +
                    " 盏（Global " + globalLights + " 盏）");
            }
            else if (!lights[0].enabled)
            {
                issues.Add("预置体的 Global Light2D 未启用");
            }

            Volume[] volumes = root.GetComponentsInChildren<Volume>(true);

            if (volumes.Length != 1)
            {
                issues.Add("预置体应恰好有 1 个 Volume，实际 " + volumes.Length + " 个");
            }
            else
            {
                if (!volumes[0].isGlobal)
                {
                    issues.Add("预置体的 Volume 不是 Global");
                }

                if (!volumes[0].enabled)
                {
                    issues.Add("预置体的 Volume 未启用");
                }

                if (volumes[0].sharedProfile == null)
                {
                    issues.Add("预置体的 Volume 未绑定 Profile");
                }
            }

            if (directors.Length == 1)
            {
                SerializedObject serialized = new SerializedObject(directors[0]);
                SerializedProperty globalLight = serialized.FindProperty("globalLight");

                if (globalLight == null || globalLight.objectReferenceValue == null)
                {
                    issues.Add("EnvironmentDirector.globalLight 未接线");
                }
                else
                {
                    Component lightComponent = globalLight.objectReferenceValue as Component;

                    if (lightComponent == null
                        || !(lightComponent.transform == root.transform
                             || lightComponent.transform.IsChildOf(root.transform)))
                    {
                        issues.Add("EnvironmentDirector.globalLight 指向预置体外部");
                    }
                }
            }

            if (cursors.Length == 1)
            {
                SerializedObject serialized = new SerializedObject(cursors[0]);
                SerializedProperty targetGrid = serialized.FindProperty("targetGrid");

                if (targetGrid != null && targetGrid.objectReferenceValue != null)
                {
                    issues.Add("TileCursorIndicator.targetGrid 必须为空");
                }
            }

            if (root.GetComponentsInChildren<Canvas>(true).Length > 0)
            {
                issues.Add("预置体不得包含 Canvas：UI 由 UIMgr 常驻管理");
            }

            if (root.GetComponentsInChildren<Camera>(true).Length > 0)
            {
                issues.Add("预置体不得包含 Camera：主相机必须留在关卡");
            }

            if (root.GetComponentsInChildren<GridComponent>(true).Length > 0)
            {
                issues.Add("预置体不得包含 Grid：指示器必须在运行时寻址关卡 Grid");
            }

            if (HasComponentByTypeName(root, "LowHealthScreenEffect"))
            {
                issues.Add("预置体不得包含 LowHealthScreenEffect：残血 UI 属于常驻 GamePlayPanel");
            }

            if (root.GetComponentsInChildren<WeaponAimController>(true).Length > 0)
            {
                issues.Add("环境预置体不得包含 WeaponAimController：Weapon 作为独立场景根部署");
            }

            return issues.Count == before;
        }

        private static bool ValidateWeaponPrefab(GameObject prefab, List<string> issues)
        {
            int before = issues.Count;

            if (prefab == null)
            {
                issues.Add("Weapon 预置体为空");
                return false;
            }

            if (!prefab.activeSelf)
            {
                issues.Add("Weapon 预置体根未激活");
            }

            WeaponAimController[] controllers =
                prefab.GetComponentsInChildren<WeaponAimController>(true);

            if (controllers.Length != 1)
            {
                issues.Add("Weapon 预置体应恰好有 1 个 WeaponAimController，实际 " + controllers.Length + " 个");
            }
            else if (!controllers[0].enabled)
            {
                issues.Add("WeaponAimController 未启用");
            }

            if (prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 0)
            {
                issues.Add("Weapon 预置体没有 SpriteRenderer");
            }

            return issues.Count == before;
        }

        private static bool ValidateGlobalLowHealthUi()
        {
            GameObject panel = AssetDatabase.LoadAssetAtPath<GameObject>(GamePlayPanelPath);

            if (panel == null)
            {
                Debug.LogWarning("[LevelEnvironment] 找不到 GamePlayPanel：" + GamePlayPanelPath);
                return false;
            }

            if (!HasComponentByTypeName(panel, "LowHealthScreenEffect"))
            {
                Debug.LogWarning("[LevelEnvironment] GamePlayPanel 中没有 LowHealthScreenEffect。");
                return false;
            }

            return true;
        }

        private static bool HasComponentByTypeName(GameObject root, string typeName)
        {
            if (root == null) return false;

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void ResetTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static void LogIssues(string title, List<string> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                Debug.LogError("[LevelEnvironment] " + title + "：" + issues[i]);
            }
        }

        private static string FormatIssues(List<string> issues)
        {
            return issues.Count == 0 ? "无" : string.Join("；", issues.ToArray());
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                results.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static List<GameObject> FindSceneObjectsByName(Scene scene, string name)
        {
            List<GameObject> results = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);

                for (int j = 0; j < transforms.Length; j++)
                {
                    if (string.Equals(transforms[j].name, name, StringComparison.Ordinal))
                    {
                        results.Add(transforms[j].gameObject);
                    }
                }
            }

            return results;
        }

        private static int CountConflicts(Scene scene, Transform presetRoot)
        {
            return CountGlobalLightsOutside(scene, presetRoot)
                   + CountComponentsOutside<Volume>(scene, presetRoot)
                   + CountComponentsOutside<EnvironmentDirector>(scene, presetRoot)
                   + CountComponentsOutside<TileGridLightmap>(scene, presetRoot)
                   + CountComponentsOutside<TileCursorIndicator>(scene, presetRoot);
        }

        private static int CountComponentsOutside<T>(Scene scene, Transform presetRoot)
            where T : Component
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                T[] components = roots[i].GetComponentsInChildren<T>(true);

                for (int j = 0; j < components.Length; j++)
                {
                    if (IsInsidePreset(components[j], presetRoot)) continue;

                    count++;
                }
            }

            return count;
        }

        private static int CountGlobalLightsOutside(Scene scene, Transform presetRoot)
        {
            int count = 0;
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                Light2D[] lights = roots[i].GetComponentsInChildren<Light2D>(true);

                for (int j = 0; j < lights.Length; j++)
                {
                    if (lights[j].lightType != Light2D.LightType.Global) continue;
                    if (IsInsidePreset(lights[j], presetRoot)) continue;

                    count++;
                }
            }

            return count;
        }

        private static bool IsInsidePreset(Component component, Transform presetRoot)
        {
            if (component == null || presetRoot == null) return false;

            Transform transform = component.transform;

            return transform == presetRoot || transform.IsChildOf(presetRoot);
        }

        private static bool IsPrefabInstanceOf(GameObject instance, GameObject prefab)
        {
            if (instance == null || prefab == null) return false;
            if (!PrefabUtility.IsPartOfPrefabInstance(instance)) return false;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);

            return source == prefab;
        }

        private static bool IsEditorBusy()
        {
            return EditorApplication.isPlayingOrWillChangePlaymode
                   || EditorApplication.isCompiling
                   || EditorApplication.isUpdating
                   || BuildPipeline.isBuildingPlayer;
        }

        private bool CanStart()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[LevelEnvironment] 工具正在执行。");
                return false;
            }

            if (IsEditorBusy())
            {
                Debug.LogWarning("[LevelEnvironment] 编辑器忙（播放/编译/构建），已中止。");
                return false;
            }

            if (_presetPrefab == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_presetPrefab)))
            {
                Debug.LogError("[LevelEnvironment] 未指定有效的预置体资产。");
                return false;
            }

            if (!_isDryRun && !_confirmGitClean)
            {
                Debug.LogError("[LevelEnvironment] 未确认 Git 工作区洁净，已中止。");
                return false;
            }

            return true;
        }

        private static void CollectFolderScenes(string folderPath, List<string> scenePaths)
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { folderPath });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!IsLevelScenePath(path)) continue;

                AddScenePath(path, scenePaths);
            }
        }

        private static void AddScenePath(string path, List<string> scenePaths)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return;

            if (string.Equals(path, RangeScenePath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[LevelEnvironment] 已跳过基准场景 Range.unity。");
                return;
            }

            if (!scenePaths.Contains(path))
            {
                scenePaths.Add(path);
            }
        }

        private static List<string> NormalizeScenePaths(List<string> scenePaths)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < scenePaths.Count; i++)
            {
                if (seen.Add(scenePaths[i]))
                {
                    result.Add(scenePaths[i]);
                }
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static bool IsLevelScenePath(string path)
        {
            return path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                   && Path.GetFileName(path).StartsWith("Level_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (string.Equals(scenes[i].path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RestoreOriginalScene(string originalScenePath)
        {
            if (string.IsNullOrEmpty(originalScenePath))
            {
                Debug.LogWarning("[Deploy] 原场景未命名，保留最后一个已处理场景。");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScenePath) == null)
            {
                Debug.LogWarning("[Deploy] 原场景资产不存在，无法切回：" + originalScenePath);
                return;
            }

            try
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deploy] 切回原场景失败：" + ex.Message);
            }
        }

        private sealed class ScenePlan
        {
            public string Path;
            public Camera Camera;
            public GameObject PresetRoot;
            public bool CanInject;
            public bool CanInjectWeapon;
            public bool CanUpdateCamera;
            public bool NeedsOrderFix;
            public bool IsSkip;
            public bool IsBlocked;
            public bool CameraNeedsChange;
            public readonly List<string> Issues = new List<string>();
            public readonly List<string> Warnings = new List<string>();
        }
    }
}
#endif
