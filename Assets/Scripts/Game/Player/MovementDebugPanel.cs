using DeepSeaOil.Logic;
using DeepSeaOil.Logic.Events;
using DeepSeaOil.Logic.Movement;
using DeepSeaOil.Logic.Player;
using UnityEngine;

namespace DeepSeaOil.Presentation
{
    /// <summary>
    /// 移动调试面板：显示当前移动状态、帧首真值与本帧提交量、引擎回读速度与环境检测结果。
    /// </summary>
    /// <remarks>
    /// 状态机首次进入不发事件，故状态一律直接读 <see cref="PlayerLogic.CurrentState"/> 补显，
    /// 订阅 <see cref="MovementStateChanged"/> 只用于显示切换历史。
    /// "提交后预期"与"引擎速度"不一致即引擎在干预（落地接触、撞墙、外力），属正常；
    /// 引擎速度是上一物理步结束时的值，滞后一帧。
    /// </remarks>
    public sealed class MovementDebugPanel : MonoBehaviour
    {
        [SerializeField] private PlayerController controller = default;
        [SerializeField] private Vector2 panelOrigin = new Vector2(8f, 8f);
        [SerializeField] private Vector2 panelSize = new Vector2(360f, 165f);
        [SerializeField] private bool isPanelVisible = true;

        private MovementStateTag _previous;
        private MovementStateTag _last;
        private int _changeCount;

        private void Start()
        {
            EventBus<MovementStateChanged>.Subscribe(OnStateChanged);
        }

        private void OnDestroy()
        {
            EventBus<MovementStateChanged>.Unsubscribe(OnStateChanged);
        }

        private void OnStateChanged(MovementStateChanged evt)
        {
            _previous = evt.Previous;
            _last = evt.Current;
            _changeCount++;
        }

        private void OnGUI()
        {
            if (!isPanelVisible) return;

            GUILayout.BeginArea(new Rect(panelOrigin.x, panelOrigin.y, panelSize.x, panelSize.y), GUI.skin.box);

            PlayerLogic logic = controller == null ? null : controller.Logic;
            if (logic == null)
            {
                GUILayout.Label("MovementDebugPanel: 未接线");
                GUILayout.EndArea();
                return;
            }

            WorldInfo world = controller.World;
            Vector2 frameStart = logic.FrameStartVelocity;
            Vector2 submitted = logic.SubmittedDelta;
            Vector2 expected = frameStart + submitted;
            Vector2 engine = controller.EngineVelocity;

            GUILayout.Label($"状态: {logic.CurrentState}");
            GUILayout.Label(_changeCount == 0 ? "切换: (首帧)" : $"切换: {_previous} → {_last} ×{_changeCount}");
            GUILayout.Label($"帧首真值 v: ({frameStart.x:F2}, {frameStart.y:F2})");
            GUILayout.Label($"本帧 Δv: ({submitted.x:F2}, {submitted.y:F2})");
            GUILayout.Label($"提交后预期: ({expected.x:F2}, {expected.y:F2})");
            GUILayout.Label($"引擎速度: ({engine.x:F2}, {engine.y:F2})");
            GUILayout.Label($"地面: {world.Grounded}   贴墙: {world.TouchingWall} (侧 {world.WallSide})");

            GUILayout.EndArea();
        }
    }
}
