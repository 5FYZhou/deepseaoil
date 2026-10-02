// ---------------------------------------------------------------------------
// 俯视角移动 + 相机接线校验（编辑器菜单工具，不进构建产物）
//
// 【为什么放在这里而不是 Assets/Editor/】
//   本工具要读 PlayerController / MovementMotor 的具体类型（编译期检查才有意义）。
//   但 Assets/Editor/ 被 DeepseaOil.EditorTools.asmdef 覆盖，而 asmdef 程序集
//   **无法**引用预定义程序集 Assembly-CSharp —— 运行时脚本全都在那里面，
//   所以放进 Assets/Editor/ 会直接编译不过（CS0234）。
//   与 Data层Tests.cs / 移动Tests.cs 用的是同一条通路：
//   本目录没有 asmdef，靠「路径里有名为 Editor 的目录」落 Assembly-CSharp-Editor，
//   它既能引用 Assembly-CSharp，也能用 UnityEditor（菜单项就是这个来源）。
//   ⚠️ 末级目录名 Editor 是 Unity 硬要求，不能改名。
//
// 【为什么要它】这一阶段的坑全都不是"代码错"，而是"场景里少拖了一个引用"：
//   少接 PlayerController.boundsArea   → 玩家走出地图（不报错）
//   少加 CinemachineBrain              → 相机完全不动（不报错）
//   vcam 的 Follow 为空                → 相机停在原点（不报错）
//   Confiner 的 Bounding Shape 为空    → 相机不被边界限制（不报错）
// 这几件事 Unity 一个都不会提醒，所以在这里一次性问清楚。
//
// 【为什么 Cinemachine 那几个用类型全名匹配而不是直接引用类型】
//   把"编辑器工具能不能编译"绑死在第三方包上不值得；装没装 Cinemachine 都能编译，
//   装了就查得更细。
//
// 跑法：菜单 Tools ▸ 深海石油 ▸ 校验移动与相机接线
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DeepseaOil.Presentation;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public static class MovementSetupCheck
    {
        private const string CinemachineBrainType = "Cinemachine.CinemachineBrain";
        private const string CinemachineVcamType = "Cinemachine.CinemachineVirtualCamera";
        private const string CinemachineConfiner2DType = "Cinemachine.CinemachineConfiner2D";

        [MenuItem("Tools/深海石油/校验移动与相机接线")]
        public static void Validate()
        {
            var report = new StringBuilder();
            int failures = 0;

            failures += CheckPlayer(report);
            failures += CheckBounds(report);
            failures += CheckCamera(report);
            failures += CheckCinemachine(report);
            failures += CheckSerializedContract(report);

            if (failures == 0)
            {
                Debug.Log("[移动接线校验] 全部通过：\n" + report);
            }
            else
            {
                Debug.LogError($"[移动接线校验] {failures} 项未通过：\n" + report);
            }
        }

        // ---------------------------------------------------------------- 玩家

        private static int CheckPlayer(StringBuilder report)
        {
            int failures = 0;

            MovementMotor[] motors = FindAll<MovementMotor>();
            if (motors.Length == 0)
            {
                report.AppendLine("玩家 · 未找到带 MovementMotor 的物体");
                return 1;
            }

            if (motors.Length > 1)
                report.AppendLine($"玩家 · 找到 {motors.Length} 个 MovementMotor，只应有一个");

            GameObject player = motors[0].gameObject;

            if (player.GetComponent<Rigidbody2D>() == null)
            {
                report.AppendLine($"玩家 · {player.name} 缺 Rigidbody2D");
                failures++;
            }

            if (player.GetComponent<Collider2D>() == null)
            {
                report.AppendLine($"玩家 · {player.name} 没有任何 Collider2D，障碍物挡不住它");
                failures++;
            }

            PlayerController controller = player.GetComponent<PlayerController>();
            if (controller == null)
                report.AppendLine($"玩家 · {player.name} 缺 PlayerController（移动逻辑不会被驱动）");
            else
                report.AppendLine($"玩家 · {player.name} 组件齐全");

            return failures;
        }

        // ---------------------------------------------------------------- 边界

        private static int CheckBounds(StringBuilder report)
        {
            int failures = 0;

            PlayerController[] controllers = FindAll<PlayerController>();
            if (controllers.Length == 0) return 0;

            var serialized = new SerializedObject(controllers[0]);
            SerializedProperty boundsProperty = serialized.FindProperty("boundsArea");
            var bounds = boundsProperty == null ? null : boundsProperty.objectReferenceValue as BoxCollider2D;

            if (bounds == null)
            {
                report.AppendLine("边界 · PlayerController.boundsArea 未接线：玩家不会被限制在地图内（不报错，只是走出去了）");
                return 1;
            }

            report.AppendLine($"边界 · 已接线 → {bounds.gameObject.name}");

            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            {
                report.AppendLine($"边界 · {bounds.gameObject.name} 的 Size = {bounds.size}，含 0 → 会被判为无效区域、不钳位");
                return 1;
            }

            // 边界框若是实体碰撞体，它会把玩家（出生点在地图中心）整个包住，
            // 物理引擎会不停把它往外解算 —— 与钳位互为第二个写者。
            if (!bounds.isTrigger)
            {
                report.AppendLine(
                    $"边界 · {bounds.gameObject.name} 的 Is Trigger 是关的："
                    + "它会把玩家包在实体碰撞体里，请勾上 Is Trigger（区域标记不该参与碰撞）"
                    );
                failures++;
            }

            return failures;
        }

        // ---------------------------------------------------------------- 相机

        private static int CheckCamera(StringBuilder report)
        {
            int failures = 0;

            Camera main = Camera.main;
            if (main == null)
            {
                report.AppendLine("相机 · 场景里没有 MainCamera 标签的相机");
                return 1;
            }

            if (!main.orthographic)
            {
                report.AppendLine("相机 · Main Camera 不是 Orthographic（俯视角 2D 应为正交）");
                failures++;
            }

            MonoBehaviour[] vcams = FindByTypeName(CinemachineVcamType);
            if (vcams.Length == 0)
            {
                report.AppendLine("相机 · 场景里没有 CinemachineVirtualCamera：相机不会跟随玩家");
                return failures + 1;
            }

            if (vcams.Length > 1)
            {
                // 两个 vcam 会争夺同一个 Brain，行为取决于优先级/激活顺序。
                // 本工具只检查第一个，所以这里必须显式报出来——否则是一个看不见的隐患。
                var names = new StringBuilder();
                foreach (MonoBehaviour vcam in vcams) names.Append(vcam.gameObject.name).Append(' ');
                report.AppendLine($"相机 · 场景里有 {vcams.Length} 个 vcam（{names}）：只应留一个，多余的手动删掉");
                failures++;
            }

            Transform follow = null;
            var vcamSerialized = new SerializedObject(vcams[0]);
            SerializedProperty followProperty = vcamSerialized.FindProperty("m_Follow");
            if (followProperty != null)
                follow = followProperty.objectReferenceValue as Transform;

            if (follow == null)
            {
                report.AppendLine($"相机 · {vcams[0].gameObject.name} 的 Follow 为空：相机停在原点");
                failures++;
            }
            else if (follow.GetComponent<Rigidbody2D>() == null)
            {
                report.AppendLine($"相机 · Follow = {follow.name}，但它没有 Rigidbody2D（应指向玩家本体）");
                failures++;
            }
            else
            {
                report.AppendLine($"相机 · {vcams[0].gameObject.name} Follow → {follow.name}");
            }

            // vcam 必须是根对象：挂到别的物体下面时世界 z 会被父级叠加，相机会跑到玩家背后。
            if (vcams[0].transform.parent != null)
            {
                report.AppendLine(
                    $"相机 · {vcams[0].gameObject.name} 挂在 {vcams[0].transform.parent.name} 下面，"
                    + $"世界坐标 z = {vcams[0].transform.position.z}：它必须是根对象，否则相机跑到玩家背后"
                    );
                failures++;
            }

            Component confiner = FindComponentByName(vcams[0].gameObject, CinemachineConfiner2DType);
            if (confiner == null)
            {
                report.AppendLine("相机 · vcam 上没有 CinemachineConfiner2D：相机不会被地图边界钳住（Extensions ▸ Add Extension 里加）");
                failures++;
            }
            else
            {
                var confinerSerialized = new SerializedObject(confiner);
                SerializedProperty shape = confinerSerialized.FindProperty("m_BoundingShape2D");
                if (shape == null || shape.objectReferenceValue == null)
                {
                    report.AppendLine("相机 · Confiner2D 的 Bounding Shape 2D 为空：相机不会被边界钳住");
                    failures++;
                }
                else
                {
                    var shapeComponent = shape.objectReferenceValue as Component;
                    report.AppendLine($"相机 · Confiner2D 边界形状 → {shapeComponent?.gameObject.name}");

                    // Cinemachine 2.x 的 Confiner2D 只接受多边形/复合碰撞体；
                    // 拖成 BoxCollider2D 时 Inspector 会报红字，而相机其实没被限制。
                    if (shapeComponent != null
                        && !(shapeComponent is PolygonCollider2D)
                        && !(shapeComponent is CompositeCollider2D))
                    {
                        report.AppendLine(
                            $"相机 · Confiner2D 的形状是 {shapeComponent.GetType().Name}，"
                            + "只接受 PolygonCollider2D / CompositeCollider2D（当前它不会生效）"
                            );
                        failures++;
                    }
                }
            }

            return failures;
        }

        // ---------------------------------------------------------------- 场景契约
        //
        // 【为什么查这个】[SerializeField] 字段一改名，场景 YAML 里的键就成了孤儿、引用静默变成
        //   {fileID: 0}，Unity 不报任何错，直到运行时才表现为"不动"。
        //   本轮已实打实踩过一次（boundsArea / jumpBufferTime → inputBufferTime）。

        /// <summary>本工程的 MonoBehaviour，逐个核对它在场景里的序列化字段。</summary>
        private static readonly (Type Type, string Name)[] ContractTargets =
        {
            (typeof(PlayerController), "PlayerController"),
            (typeof(MovementMotor), "MovementMotor"),
            (typeof(MovementDebugPanel), "MovementDebugPanel"),
            (typeof(InputProvider), "InputProvider"),
        };

        /// <summary>有意留空、允许引用为 {fileID: 0} 的字段（运行时惰性自取，不算未接线）。</summary>
        private static readonly HashSet<string> IntentionallyUnwired =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MovementMotor.body",
            };

        /// <summary>
        /// 不参与判定的历史场景：里面的断链只提示、不判失败。
        /// </summary>
        /// <remarks>
        /// SampleScene 是平台跳跃时代的场景，那边的 PlayerController 本就没接 boundsArea；
        /// 在此判失败会让校验永远红着，反而教会人忽略它。
        /// 移动/相机的验收场景是 TestPhysics，它那边的断链照旧判失败。
        /// 将来给 TestPhysics 改名时别忘了同步这里。
        /// </remarks>
        private const string SceneUnderTest = "TestPhysics";

        private static int CheckSerializedContract(StringBuilder report)
        {
            int failures = 0;
            var scenes = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    scenes.Add(path);
                }
            }

            if (scenes.Count == 0)
            {
                report.AppendLine("契约 · 工程里没有场景，跳过");
                return 0;
            }

            foreach ((Type type, string name) in ContractTargets)
            {
                string scriptGuid = ScriptGuidOf(type);
                if (scriptGuid == null)
                {
                    report.AppendLine($"契约 · {name} 找不到 .cs.meta，无法核对（请手工确认）");
                    failures++;
                    continue;
                }

                List<string> fields = SerializedFieldsOf(type);
                if (fields.Count == 0) continue;   // 没有序列化字段就没什么可核对的

                int unwired = 0;
                var notes = new List<string>();

                foreach (string scene in scenes)
                {
                    string text = File.ReadAllText(scene);
                    string needle = "m_Script: {fileID: 11500000, guid: " + scriptGuid;

                    if (text.IndexOf(needle, StringComparison.Ordinal) < 0) continue;   // 该场景没有这个组件

                    string sceneName = Path.GetFileNameWithoutExtension(scene);
                    bool isSceneUnderTest = string.Equals(sceneName, SceneUnderTest, StringComparison.Ordinal);
                    int sceneWired = 0;
                    int sceneUnwired = 0;

                    foreach (string field in fields)
                    {
                        string key = field + ":";
                        int index = text.IndexOf(key, StringComparison.Ordinal);

                        if (index < 0)
                        {
                            notes.Add($"契约 · {name}.{field} 在 {sceneName} 里没有对应键（该场景可能尚未被 Unity 重新序列化）");
                            continue;
                        }

                        int lineEnd = text.IndexOf('\n', index);
                        string line = lineEnd < 0 ? text.Substring(index) : text.Substring(index, lineEnd - index);

                        if (!line.Contains("fileID:"))
                        {
                            sceneWired++;   // 值类型字段，没有引用可断
                            continue;
                        }

                        if (line.Contains("fileID: 0"))
                        {
                            if (IntentionallyUnwired.Contains(name + "." + field))
                            {
                                sceneWired++;
                                notes.Add($"契约 · {name}.{field} 未接线（有意：运行时惰性自取）");
                            }
                            else if (isSceneUnderTest)
                            {
                                sceneUnwired++;
                                notes.Add($"契约 · {name}.{field} 在 {sceneName} 里未接线（引用是空的）");
                            }
                            else
                            {
                                sceneWired++;
                                notes.Add(
                                    $"契约 · {name}.{field} 在 {sceneName} 里未接线"
                                    + "（该场景不是验收场景，不计失败；要让本工具管它，请把它加进 SceneUnderTest）");
                            }
                        }
                        else
                        {
                            sceneWired++;
                        }
                    }

                    unwired += sceneUnwired;

                    report.AppendLine(
                        $"契约 · {name} @ {sceneName}：已接线 {sceneWired} 项"
                        + (sceneUnwired > 0 ? $"，未接线 {sceneUnwired} 项" : string.Empty)
                        + (isSceneUnderTest ? "　← 验收场景" : string.Empty));
                }

                failures += unwired;

                // 同一个提示只报一次（多场景时上面会重复收集）
                foreach (string note in notes.Distinct()) report.AppendLine(note);
            }

            return failures;
        }

        /// <summary>抽出 <c>[SerializeField]</c> 字段名；支持下方的三种写法与多字段声明。</summary>
        private static List<string> SerializedFieldsOf(Type type)
        {
            var result = new List<string>();

            MonoScript script = FindScriptAsset(type);
            if (script == null) return result;

            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path)) return result;

            string code = File.ReadAllText(path);

            // 覆盖三种写法：① 属性与字段同行；② 属性独占一行、字段在下一行；③ 一行声明多个字段
            var pattern = new Regex(@"\[SerializeField\][\s\S]{0,200}?;\s*");

            foreach (Match match in pattern.Matches(code))
            {
                string body = match.Value;
                int semicolon = body.LastIndexOf(';');
                if (semicolon > 0) body = body.Substring(0, semicolon);

                int equals = body.IndexOf('=');
                if (equals > 0) body = body.Substring(0, equals);

                // 去掉属性与修饰符，剩下的形如 "Rigidbody2D body" 或 "float a, b, c"
                string[] parts = body.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool sawType = false;

                foreach (string raw in parts)
                {
                    string part = raw.TrimEnd(',');

                    if (part.Length == 0) continue;
                    if (part.IndexOf('[') >= 0 || part.IndexOf(']') >= 0) continue;   // 属性自身的碎片
                    if (part == "private" || part == "public" || part == "protected" || part == "internal"
                        || part == "static" || part == "readonly" || part == "volatile" || part == "const") continue;

                    // 第一个标识符是类型，之后每个标识符都是字段名
                    if (!sawType)
                    {
                        sawType = true;
                        continue;
                    }

                    if (!IsIdentifier(part)) continue;
                    if (!result.Contains(part)) result.Add(part);
                }
            }

            return result;
        }

        private static bool IsIdentifier(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (!char.IsLetter(text[0]) && text[0] != '_') return false;

            foreach (char c in text)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }

            return true;
        }

        private static MonoScript FindScriptAsset(Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + type.Name + ".cs", StringComparison.Ordinal)) return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            }

            return null;
        }

        /// <summary>从 .cs.meta 里读脚本 GUID —— 场景 YAML 就是用它认组件的。</summary>
        private static string ScriptGuidOf(Type type)
        {
            MonoScript script = FindScriptAsset(type);
            if (script == null) return null;

            string path = AssetDatabase.GetAssetPath(script);
            string meta = path + ".meta";
            if (!File.Exists(meta)) return null;

            Match match = Regex.Match(
                File.ReadAllText(meta),
                @"^guid:\s*([0-9a-fA-F]+)",
                RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value : null;
        }

        // ---------------------------------------------------------------- 包

        private static int CheckCinemachine(StringBuilder report)
        {
            Type brain = FindType(CinemachineBrainType);
            if (brain == null)
            {
                report.AppendLine("包 · 未找到 Cinemachine（Window ▸ Package Manager 里装 com.unity.cinemachine）");
                return 1;
            }

            Camera main = Camera.main;
            if (main != null && main.GetComponent(brain) == null)
            {
                report.AppendLine("包 · Main Camera 上没有 CinemachineBrain：vcam 不会驱动相机");
                return 1;
            }

            return 0;
        }

        // ---------------------------------------------------------------- 工具

        private static T[] FindAll<T>() where T : Component
        {
            // 工程锁定 Unity 2022.3.62f3c1 LTS，用这一版稳定存在的重载（含非激活物体）。
            return UnityEngine.Object.FindObjectsOfType<T>(true);
        }

        private static MonoBehaviour[] FindByTypeName(string typeName)
        {
            var result = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in FindAll<MonoBehaviour>())
            {
                if (Matches(behaviour, typeName))
                    result.Add(behaviour);
            }
            return result.ToArray();
        }

        private static Component FindComponentByName(GameObject go, string typeName)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component != null && Matches(component, typeName))
                    return component;
            }
            return null;
        }

        private static bool Matches(Component component, string typeName)
        {
            Type type = component.GetType();
            return type.FullName == typeName || type.Name == typeName;
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}