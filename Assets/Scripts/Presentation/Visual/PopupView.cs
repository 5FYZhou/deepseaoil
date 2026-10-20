using TMPro;
using UnityEngine;

namespace DeepseaOil.Presentation.Visual
{
    /// <summary>飘字弹窗门面：驱动只调本类，不碰子节点。加边框/图标时改 prefab + 本类，驱动零改动</summary>
    public sealed class PopupView : MonoBehaviour
    {
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        private static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");

        [SerializeField] private TMP_Text label;

        private Material _outlineMaterial;
        private float _baseFontSize;
        private float _appliedOutlineWidth = -1f;
        private float _appliedFontScale = -1f;
        private Color _appliedOutlineColor;

        private bool _materialChecked;
        private bool _faceColorOn;

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

        /// <summary>只改 alpha：写实例材质 _FaceColor/_OutlineColor，写顶点色会每帧重建网格</summary>
        public void SetAlpha(float alpha)
        {
            if (label == null) return;

            if (!_faceColorOn)
            {
                Color vertex = label.color;
                vertex.a = alpha;

                label.color = vertex;

                return;
            }

            _outlineMaterial.SetColor(FaceColorId, new Color(1f, 1f, 1f, alpha));

            Color outline = _appliedOutlineColor;
            outline.a = alpha;

            _outlineMaterial.SetColor(OutlineColorId, outline);
        }

        private void EnsureOutlineMaterial()
        {
            if (_materialChecked) return;

            _materialChecked = true;

            Material source = label.fontSharedMaterial;

            if (source == null || !source.HasProperty(OutlineWidthId)) return;

            _outlineMaterial = new Material(source) { name = source.name + "_Outline" };

            label.fontSharedMaterial = _outlineMaterial;

            _faceColorOn = _outlineMaterial.HasProperty(FaceColorId);
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
