using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Movement;
using DeepseaOil.Logic.Player;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 移动调试面板：显示当前移动状态、帧首真值与本帧提交量、引擎回读速度与边界接线情况。
    /// </summary>
    /// <remarks>
    /// 状态机首次进入不发事件，故状态一律直接读 <see cref="PlayerLogic.CurrentState"/> 补显，
    /// 订阅 <see cref="MovementStateChanged"/> 只用于显示切换历史。
    /// "提交后预期"与"引擎速度"不一致即引擎在干预（撞障碍物、外力），属正常；
    /// 引擎速度是上一物理步结束时的值，滞后一帧。
    /// "世界边界"一行是接线成败的唯一可见指示——未接线时不钳位且这里显示"未接线"。
    /// </remarks>
    public sealed class MovementDebugPanel : MonoBehaviour
    {
        [SerializeField] private PlayerController controller = default;
        [SerializeField] private Vector2 panelOrigin = new Vector2(8f, 8f);
        [SerializeField] private Vector2 panelSize = new Vector2(440f, 190f);
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
            GUILayout.Label($"输入方向: ({world.MoveDirection.x:F2}, {world.MoveDirection.y:F2})   朝向: ({logic.Facing.x:F2}, {logic.Facing.y:F2})");
            GUILayout.Label($"帧首真值 v: ({frameStart.x:F2}, {frameStart.y:F2})");
            GUILayout.Label($"本帧 Δv: ({submitted.x:F2}, {submitted.y:F2})");
            GUILayout.Label($"提交后预期: ({expected.x:F2}, {expected.y:F2})");
            GUILayout.Label($"引擎速度: ({engine.x:F2}, {engine.y:F2})   速率: {engine.magnitude:F2}");

            BoundsArea bounds = world.Bounds;
            GUILayout.Label(bounds.IsValid
                ? $"世界边界: 已启用  {bounds.World.min} ~ {bounds.World.max}"
                : "世界边界: 未接线（不钳位）");

            GUILayout.EndArea();
        }
    }
}
