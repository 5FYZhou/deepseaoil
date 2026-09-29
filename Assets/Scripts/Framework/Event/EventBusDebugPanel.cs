using System;
using UnityEngine;

namespace DeepSeaOil.Presentation
{
    /// <summary>
    /// EventBus 可视调试面板：在屏幕上显示最近收到的调试事件。
    /// </summary>
    /// <remarks>
    /// <para><b>所属程序集约束</b>：本类位于 <c>Dasuus.Presentation</c>，因为 <c>OnGUI</c> 依赖 <c>UnityEngine.IMGUIModule</c>。
    /// Logic 层禁止引用 UI/IMGUI，因此这类可视化代码一律留在表现层。</para>
    /// <para><b>M0 定位</b>：仅用于肉眼确认事件流是否工作，<b>不属于 M0 验收依据</b>
    /// （验收以 EditMode/PlayMode 测试为准，见 Docs/架构约束.md）。</para>
    /// <para><b>使用方式</b>：把本组件挂到任意场景物体上，进入 Play 后左上角显示面板。</para>
    /// </remarks>
    public sealed class EventBusDebugPanel : MonoBehaviour
    {
        /// <summary>日志区显示的事件条数上限。</summary>
        [SerializeField, Min(1)]
        private int maxLogLines = 8;

        /// <summary>面板显示的屏幕位置（像素）。</summary>
        [SerializeField]
        private Vector2 panelOrigin = new Vector2(8f, 8f);

        /// <summary>面板尺寸（像素）。负值表示由 GUILayout 自适应。</summary>
        [SerializeField]
        private Vector2 panelSize = new Vector2(420f, 160f);

        /// <summary>面板是否可见。</summary>
        [SerializeField]
        private bool isPanelVisible = true;

        private readonly string[] _logLines = new string[32];
        private int _logCount;
        private int _receivedCount;
        private string _lastMessage = "(尚未收到事件)";

        /// <summary>累计收到的事件条数。</summary>
        public int ReceivedCount => _receivedCount;

        private void Start()
        {
            // 订阅演示事件：仅证明"表现层可订阅逻辑层事件"这一条单向依赖成立。
            DeepSeaOil.Logic.Events.EventBus<DebugEvent>.Subscribe(OnDebugEvent);
        }

        private void OnDestroy()
        {
            // 必须退订：静态事件总线不会因物体销毁而自动解除引用。
            DeepSeaOil.Logic.Events.EventBus<DebugEvent>.Unsubscribe(OnDebugEvent);
        }

        private void OnDebugEvent(DebugEvent evt)
        {
            _receivedCount++;
            _lastMessage = evt.Message;

            if (_logLines.Length == 0)
            {
                return;
            }

            _logLines[_logCount % _logLines.Length] = $"[{_receivedCount}] {evt.Message}";
            _logCount++;
        }

        private void OnGUI()
        {
            if (!isPanelVisible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(panelOrigin.x, panelOrigin.y, panelSize.x, panelSize.y), GUI.skin.box);
            GUILayout.Label($"EventBus 调试面板 · 收到 {_receivedCount} 条");
            GUILayout.Label($"最近: {_lastMessage}");

            for (int i = 0; i < Math.Min(_logCount, maxLogLines); i++)
            {
                int index = _logCount - 1 - i;
                string line = _logLines[index % _logLines.Length];
                if (!string.IsNullOrEmpty(line))
                {
                    GUILayout.Label(line);
                }
            }

            GUILayout.EndArea();
        }

        /// <summary>
        /// 供手工验证使用的演示事件（后续被真实事件取代后即删除）。
        /// </summary>
        /// <remarks>按 F-04 约定设计为 readonly struct。</remarks>
        public readonly struct DebugEvent
        {
            public readonly string Message;

            public DebugEvent(string message)
            {
                Message = message;
            }
        }
    }
}
