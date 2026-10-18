using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    /// <summary>飘字弹窗门面：驱动只调本类，不碰子节点。加边框/图标时改 prefab + 本类，驱动零改动</summary>
    public sealed class PopupView : MonoBehaviour
    {
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        [SerializeField] private TMP_Text label;

        private Material _outlineMaterial;
        private float _baseFontSize;
        private float _appliedOutlineWidth = -1f;
        private float _appliedFontScale = -1f;
        private Color _appliedOutlineColor;

        public void SetStyle(float outlineWidth, Color outlineColor, float fontScale)
        {
            if (label == null) return;

            EnsureOutlineMaterial();

            float scale = fontScale > 0f ? fontScale : 1f;

            if (!Mathf.Approximately(_appliedFontScale, scale))
            {
                if (_baseFontSize <= 0f) _baseFontSize = label.fontSize;

                label.fontSize = _baseFontSize * scale;
                _appliedFontScale = scale;
            }

            if (_outlineMaterial == null) return;

            if (!Mathf.Approximately(_appliedOutlineWidth, outlineWidth))
            {
                _outlineMaterial.SetFloat(OutlineWidthId, outlineWidth);
                _appliedOutlineWidth = outlineWidth;
            }

            if (_appliedOutlineColor != outlineColor)
            {
                _outlineMaterial.SetColor(OutlineColorId, outlineColor);
                _appliedOutlineColor = outlineColor;
            }
        }

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

        private void EnsureOutlineMaterial()
        {
            if (_outlineMaterial != null) return;

            Material source = label.fontSharedMaterial;

            if (source == null || !source.HasProperty(OutlineWidthId)) return;

            _outlineMaterial = new Material(source) { name = source.name + "_Outline" };

            label.fontSharedMaterial = _outlineMaterial;
        }

        private void OnDestroy()
        {
            if (_outlineMaterial == null) return;

            if (Application.isPlaying) Destroy(_outlineMaterial);
            else DestroyImmediate(_outlineMaterial);

            _outlineMaterial = null;
        }
    }
}
