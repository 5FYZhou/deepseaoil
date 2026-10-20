using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepseaOil.Presentation.Input
{
    /// <summary>按下归属闸：这一下按下归不归 UI 控件</summary>
    /// <remarks>不认任意 Graphic（HUD 全屏装饰图也是 raycastTarget），只认按下/拖拽的接走人。</remarks>

    public sealed class UIPointerGate
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

        private PointerEventData _probe;

        private EventSystem _probedSystem;

        public bool OwnsPress(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null) return false;

            if (!ReferenceEquals(_probedSystem, eventSystem))
            {
                _probedSystem = eventSystem;
                _probe = new PointerEventData(eventSystem);
            }

            _probe.position = screenPosition;

            eventSystem.RaycastAll(_probe, _hits);

            if (_hits.Count == 0) return false;

            // 只看最上层命中：UGUI 也只派给 pointerCurrentRaycast 的第一个
            GameObject hit = _hits[0].gameObject;

            if (hit == null) return false;

            for (Transform node = hit.transform; node != null; node = node.parent)
            {
                if (node.GetComponent<IPointerDownHandler>() != null) return true;

                if (node.GetComponent<IDragHandler>() != null) return true;
            }

            return false;
        }
    }
}
