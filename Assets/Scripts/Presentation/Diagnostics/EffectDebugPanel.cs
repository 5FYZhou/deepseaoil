using DeepseaOil.Presentation.Effects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <remarks>取鼠标必须走 Mouse.current：本工程 activeInputHandler=1，UnityEngine.Input 抛异常。→ None = 被丢弃/未注册/资源缺失，原因见 Console</remarks>
    public sealed class EffectDebugPanel : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera = default;
        [SerializeField] private bool isPanelVisible = true;
        [SerializeField] private Vector2 panelOrigin = new Vector2(8f, 210f);
        [SerializeField] private Vector2 panelSize = new Vector2(440f, 170f);

        private string _lastResult = "(未播放)";

        private void OnGUI()
        {
            if (!isPanelVisible) return;

            GUILayout.BeginArea(new Rect(panelOrigin.x, panelOrigin.y, panelSize.x, panelSize.y), GUI.skin.box);

            if (!EffectModule.IsInitialized)
            {
                GUILayout.Label("EffectDebugPanel: EffectModule 未 Init（GameRoot 没跑起来？）");
                GUILayout.EndArea();
                return;
            }

            EffectStats stats = EffectModule.GetStats();
            GUILayout.Label($"特效: 活跃 {stats.ActiveInstances} / 驱动 {stats.DriverCount} / 池内待用 {stats.PooledObjects}");
            GUILayout.Label($"最近一次播放: {_lastResult}");

            for (int i = 0; i < EffectCatalog.All.Count; i++)
            {
                EffectSpec spec = EffectCatalog.All[i];

                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"播放 {spec.Id}", GUILayout.Width(160f)))
                {
                    EffectHandle handle = EffectModule.Play(spec.Id, EffectContext.At(MouseWorld()));
                    _lastResult = $"{spec.Id} → {handle}";
                }

                GUILayout.Label($"{(spec.IsSingleton ? "单例" : "多实例")}  上限 {spec.MaxSize}  预热 {spec.Prewarm}");
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("全部清空 (CleanAll)", GUILayout.Width(160f)))
            {
                EffectModule.CleanAll();
                _lastResult = "CleanAll";
            }

            if (GUILayout.Button($"未注册探针 ({EffectId.ScreenShake})", GUILayout.Width(200f)))
            {
                EffectHandle handle = EffectModule.Play(EffectId.ScreenShake, EffectContext.Default);
                _lastResult = $"{EffectId.ScreenShake} → {handle}（Console 里应有 LogError）";
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        /// <summary>相机把鼠标投到 z=0 平面</summary>
        private Vector2 MouseWorld()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return Vector2.zero;

            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;

            Vector2 screen = mouse.position.ReadValue();

            float depth = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

            return new Vector2(world.x, world.y);
        }
    }
}
