using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    /// <summary>飘字弹窗门面：驱动只调本类，不碰子节点。加边框/图标时改 prefab + 本类，驱动零改动</summary>
    public sealed class PopupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void SetValue(int value)
        {
            if (label == null) return;

            label.text = value.ToString();
        }

        public void SetColor(Color color)
        {
            if (label == null) return;

            label.color = color;
        }

        /// <summary>只改 alpha：颜色由 SetColor 定，写整色会把元素色抹掉</summary>
        public void SetAlpha(float alpha)
        {
            if (label == null) return;

            Color color = label.color;
            color.a = alpha;

            label.color = color;
        }
    }
}
