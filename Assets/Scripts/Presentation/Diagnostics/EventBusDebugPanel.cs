using System;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.Diagnostics
{
    /// <summary>EventBus 可视调试面板：屏幕上显示最近收到的格子状态事件。只读诊断件，不属于验收依据，不影响判定。</summary>
    /// <remarks>订阅的是逻辑层真实事实 <see cref="TileStateChanged"/>；留在表现层是因为 <c>OnGUI</c> 依赖 <c>UnityEngine.IMGUIModule</c>（Logic 层禁引 UI）。</remarks>
    public sealed class EventBusDebugPanel : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int maxLogLines = 8;

        [SerializeField]
        private Vector2 panelOrigin = new Vector2(8f, 8f);

        /// <summary>面板尺寸（像素）；负值 = 由 GUILayout 自适应。</summary>
        [SerializeField]
        private Vector2 panelSize = new Vector2(420f, 160f);

        [SerializeField]
        private bool isPanelVisible = true;

        private readonly string[] _logLines = new string[32];
        private int _logCount;
        private int _receivedCount;
        private string _lastMessage = "(尚未收到事件)";

        public int ReceivedCount => _receivedCount;

        private void Start()
        {
            EventBus<TileStateChanged>.Subscribe(OnTileStateChanged);
        }

        private void OnDestroy()
        {
            // 必须退订：静态事件总线不会因物体销毁而自动解除引用。
            EventBus<TileStateChanged>.Unsubscribe(OnTileStateChanged);
        }

        private void OnTileStateChanged(TileStateChanged evt)
        {
            _receivedCount++;
            _lastMessage = $"格 {evt.Cell} → {evt.State}";

            if (_logLines.Length == 0)
            {
                return;
            }

            _logLines[_logCount % _logLines.Length] = $"[{_receivedCount}] {_lastMessage}";
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
    }
}
