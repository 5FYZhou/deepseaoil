using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    /// <summary>实体 Y-Sort 驱动：把各渲染器按锚点 y 折算成档位，档位未变不写</summary>
    /// <remarks>表现层基础设施件，与 CameraShaker 同级走 LateUpdate，不进 GameRoot 驱动表。要固定档位的件（阴影 310 / 头顶数字 560）不要配进 layers。</remarks>
    [DisallowMultipleComponent]
    public sealed class YSortAgent : MonoBehaviour
    {
        [System.Serializable]
        public struct Layer
        {
            [Tooltip("参与排序的渲染器。拖具体的 SpriteRenderer 或它所在的子节点，不要拖 Root")]
            public Renderer Target;

            [Tooltip("相对档位偏移：身体 0、眼睛 +1、武器 +2、翅膀 -1")]
            public int Offset;
        }

        [Tooltip("一个实体可以挂多个渲染器；固定档位的件不要放进来")]
        [SerializeField] private Layer[] layers = new Layer[0];

        [Tooltip("锚点 y 偏移：脚底件填 0，需要整体抬高排序的填正数")]
        [SerializeField] private float anchorOffsetY = 0f;

        private IYSortAnchor _anchor;

        private int _lastOrder = int.MinValue;

        private void Awake()
        {
            _anchor = GetComponent<IYSortAnchor>();
        }

        private void LateUpdate()
        {
            float y = (_anchor != null ? _anchor.SortAnchor.y : transform.position.y) + anchorOffsetY;

            int order = RenderOrder.ActorOrder(y);

            if (order == _lastOrder) return;

            _lastOrder = order;

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].Target != null) layers[i].Target.sortingOrder = order + layers[i].Offset;
            }
        }
    }
}
