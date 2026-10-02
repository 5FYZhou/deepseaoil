// ---------------------------------------------------------------------------
// 相机跟随诊断（编辑器菜单，不进构建产物）
//
// 【为什么要它】"镜头不跟随"有好几种成因，而它们从场景文件里看起来一模一样：
//   · Brain 没在驱动相机（Brain 丢了 / 被别的相机抢了）
//   · vcam 没被判为 live（优先级、激活状态、StandbyUpdate）
//   · Body 组件（FramingTransposer）算出的位置本身就不对
//   · Confiner 扩展把相机钳住了
// 本工具把这几件事一次性问清楚，并提供一个"临时摘掉 Confiner"的开关做对照实验。
//
// 【为什么用反射】与 MovementSetupCheck 一致：装没装 Cinemachine 都能编译，
//   装了就查得更细。这里要读的是 Cinemachine 的内部状态（ActiveVirtualCamera 等），
//   只能靠反射。
//
// 跑法：菜单 Tools ▸ 深海石油 ▸ 相机跟随诊断
//      菜单 Tools ▸ 深海石油 ▸ 开关 Confiner2D
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeepseaOil.Tests
{
    public static class MovementCameraDiagnostics
    {
        private const string BrainTypeName = "Cinemachine.CinemachineBrain";
        private const string VcamBaseTypeName = "Cinemachine.CinemachineVirtualCameraBase";
        private const string Confiner2DTypeName = "Cinemachine.CinemachineConfiner2D";

        [MenuItem("Tools/深海石油/相机跟随诊断")]
        public static void Diagnose()
        {
            var report = new StringBuilder();

            Camera main = Camera.main;
            if (main == null)
            {
                Debug.LogError("[相机诊断] 场景里没有 MainCamera 标签的相机");
                return;
            }

            report.AppendLine($"相机 · {main.name}  world={Fmt(main.transform.position)}  ortho={main.orthographic} size={main.orthographicSize}");

            // ① Brain
            Component brain = FindComponentOn(main.gameObject, BrainTypeName);
            if (brain == null)
            {
                report.AppendLine("相机 · ❌ Main Camera 上没有 CinemachineBrain —— 相机不会被任何 vcam 驱动");
            }
            else
            {
                report.AppendLine($"相机 · Brain enabled={Fmt(GetMember(brain, "enabled"))}  updateMethod={Fmt(GetMember(brain, "UpdateMethod"))}");

                object active = GetMember(brain, "ActiveVirtualCamera");
                if (active == null)
                {
                    report.AppendLine("相机 · ❌ Brain.ActiveVirtualCamera = null —— 没有任何 vcam 被判为 live");
                    report.AppendLine("       常见原因：没有启用的 vcam；或所有 vcam 的优先级都 ≤ 0");
                }
                else if (active is Component activeVcam)
                {
                    report.AppendLine($"相机 · ✅ ActiveVirtualCamera = {activeVcam.gameObject.name}（{activeVcam.GetType().Name}）");
                    report.AppendLine($"       vcam world={Fmt(activeVcam.transform.position)}  activeInHierarchy={activeVcam.gameObject.activeInHierarchy}");
                }
                else
                {
                    report.AppendLine($"相机 · ActiveVirtualCamera = {active}（非 Component）");
                }
            }

            // ② 全部 vcam
            List<Component> vcams = FindAllByName(VcamBaseTypeName);
            report.AppendLine($"vcam · 共 {vcams.Count} 个");
            foreach (Component vcam in vcams)
            {
                object follow = GetMember(vcam, "Follow");
                object priority = GetMember(vcam, "Priority");
                object owner = GetMember(vcam, "ComponentOwner");

                report.AppendLine(
                    $"   · {vcam.gameObject.name}  enabled={Fmt(GetMember(vcam, "enabled"))}"
                    + $"  priority={Fmt(priority)}"
                    + $"  Follow={Name(GetMember(vcam, "Follow") as Component)}"
                    + $"  world={Fmt(vcam.transform.position)}"
                    + $"  activeInHierarchy={vcam.gameObject.activeInHierarchy}");

                if (owner is Component ownerComponent)
                {
                    report.AppendLine($"     ComponentOwner = {ownerComponent.gameObject.name}  world={Fmt(ownerComponent.transform.position)}");
                }

                if (follow == null)
                {
                    report.AppendLine("     ❌ Follow 为空：这个 vcam 不会跟随任何东西");
                }
            }

            // ③ Confiner 对照实验开关
            foreach (Component vcam in vcams)
            {
                Component confiner = FindComponentOn(vcam.gameObject, Confiner2DTypeName);
                if (confiner == null) continue;

                report.AppendLine($"Confiner · 在 {vcam.gameObject.name} 上，enabled={Fmt(GetMember(confiner, "enabled"))}");
            }

            report.AppendLine();
            report.AppendLine("对照实验：跑一次「Tools ▸ 深海石油 ▸ 开关 Confiner2D」把 Confiner 关掉，再进 Play 移动。");
            report.AppendLine("  关掉后跟随正常 → 问题在 Confiner（边界形状或参数）");
            report.AppendLine("  关掉后仍不跟随 → 问题在 Brain / vcam / Body");

            Debug.Log("[相机诊断]\n" + report);
        }

        [MenuItem("Tools/深海石油/开关 Confiner2D")]
        public static void ToggleConfiner()
        {
            List<Component> vcams = FindAllByName(VcamBaseTypeName);
            int changed = 0;

            foreach (Component vcam in vcams)
            {
                Component confiner = FindComponentOn(vcam.gameObject, Confiner2DTypeName);
                if (confiner == null) continue;

                var behaviour = confiner as Behaviour;
                if (behaviour == null) continue;

                Undo.RecordObject(behaviour, "Toggle Confiner2D");
                behaviour.enabled = !behaviour.enabled;
                changed++;

                Debug.Log($"[相机诊断] {vcam.gameObject.name} 上的 CinemachineConfiner2D → enabled = {behaviour.enabled}");
            }

            if (changed == 0)
            {
                Debug.LogWarning("[相机诊断] 没找到任何 CinemachineConfiner2D");
                return;
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        // ---------------------------------------------------------------- 工具

        private static object GetMember(object target, string name)
        {
            if (target == null) return null;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(name, Flags);
                if (property != null) return property.GetValue(target);

                FieldInfo field = type.GetField(name, Flags);
                if (field != null) return field.GetValue(target);
            }

            return null;
        }

        private static Component FindComponentOn(GameObject go, string typeName)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                if (Matches(component.GetType(), typeName)) return component;

                // Cinemachine 把扩展挂在子物体（pipeline host）上
                foreach (Component child in component.GetComponentsInChildren(component.GetType(), true))
                {
                    if (child != null && Matches(child.GetType(), typeName)) return child;
                }
            }

            foreach (Component child in go.GetComponentsInChildren<Component>(true))
            {
                if (child != null && Matches(child.GetType(), typeName)) return child;
            }

            return null;
        }

        private static List<Component> FindAllByName(string typeName)
        {
            var result = new List<Component>();

            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour != null && Matches(behaviour.GetType(), typeName)) result.Add(behaviour);
            }

            return result;
        }

        private static bool Matches(Type type, string typeName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (t.FullName == typeName || t.Name == typeName) return true;
            }

            return false;
        }

        private static string Fmt(object value)
        {
            return value == null ? "<null>" : value.ToString();
        }

        private static string Name(Component component)
        {
            return component == null ? "<空>" : component.gameObject.name;
        }

        private static string Fmt(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }
    }
}
