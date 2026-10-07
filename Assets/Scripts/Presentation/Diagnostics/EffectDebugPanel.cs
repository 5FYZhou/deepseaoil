using DeepseaOil.Presentation.Effects;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <summary>
    /// 特效调试面板：一行统计 + 每个已装配特效一个"播放"按钮 + 全部清空。
    /// </summary>
    /// <remarks>
    /// <para><b>它是验收工具，不是游戏 UI</b>：挂在场景里任意物体上即可（建议和 <c>MovementDebugPanel</c>
    /// 同一个物体）。不挂也不影响任何功能。</para>
    /// <para><b>为什么取鼠标位置走 <c>Mouse.current</c></b>：本工程
    /// <c>ProjectSettings.activeInputHandler = 1</c>（只用新输入系统），<c>UnityEngine.Input</c> 会抛异常。</para>
    /// <para><b>按钮的用处</b>：不依赖白模就能单独验证
    /// 「能播 / 池满丢弃 / 未注册报错 / 切场景清空」这四条验收项；
    /// 按钮右边那行 <c>→ Effect#3g1</c> / <c>→ None</c> 就是播放结果的即时回执
    /// （<c>None</c> = 被丢弃 / 未注册 / 资源缺失，具体原因看 Console）。</para>
    /// </remarks>
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

            // 探针：故意用一个没有驱动的 EffectId，验证"未注册只报错不崩"（Console 里会出现一条 LogError）
            if (GUILayout.Button($"未注册探针 ({EffectId.ScreenShake})", GUILayout.Width(200f)))
            {
                EffectHandle handle = EffectModule.Play(EffectId.ScreenShake, EffectContext.Default);
                _lastResult = $"{EffectId.ScreenShake} → {handle}（Console 里应有 LogError）";
            }

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        /// <summary>鼠标当前的世界坐标（投到 z = 0 平面）。取不到相机或鼠标时返回原点。</summary>
        private Vector2 MouseWorld()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return Vector2.zero;

            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;

            Vector2 screen = mouse.position.ReadValue();

            // 正交相机（本工程是纯俯视正交）：把屏幕点投到 z = 0 平面
            float depth = -cam.transform.position.z;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

            return new Vector2(world.x, world.y);
        }
    }
}
