using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class TransitionPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Transition;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        [SerializeField] private Material material;
        [SerializeField] private Material materialInside;
        [SerializeField] private Material materialOutside;

        [Header("动画参数")]
        [SerializeField] private float duration = 0.3f;
        [SerializeField] private float maxMaskScale = 35.5f;

        [Tooltip("归一化进度(0~1) → 归一化缩放(0~1)，默认线性")]
        [SerializeField]
        private AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Image _imgMask;
        private Coroutine _playCoroutine;

        protected override void Awake()
        {
            base.Awake();
            _imgMask = GetComponent<Image>("ImgMask");
        }

        public override void ShowMe()
        {
        }

        public override void HideMe()
        {
        }


        /// 播放过渡动画




        /// <remarks>unity的Mask组件 即使在不是AB的其他面板 也会影响遮罩效果？</remarks>
        public void Play(BasePanel panelA, BasePanel panelB, Action onComplete = null)
        {
            if (panelA == null || panelB == null)
                return;

            var graphsA = panelA.GetComponentsInChildren<Graphic>();
            var graphsB = panelB.GetComponentsInChildren<Graphic>();

            foreach (var graph in graphsA)
                graph.material = materialOutside;

            foreach (var graph in graphsB)
                graph.material = materialInside;

            SetRadius(0.5f);

            _imgMask.gameObject.SetActive(true);

            if (_playCoroutine != null)
                StopCoroutine(_playCoroutine);

            _playCoroutine = StartCoroutine(PlayRoutine(panelA, panelB, onComplete));
        }

        private IEnumerator PlayRoutine(BasePanel panelA, BasePanel panelB, Action onComplete)
        {
            float startScale = 1f;
            float endScale = maxMaskScale;
            float elapsed = 0f;

            _imgMask.rectTransform.localScale = Vector3.one * startScale;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 用曲线重映射进度
                float curved = progressCurve.Evaluate(t);

                float scale = Mathf.LerpUnclamped(startScale, endScale, curved);
                _imgMask.rectTransform.localScale = Vector3.one * scale;

                yield return null;
            }

            _imgMask.rectTransform.localScale = Vector3.one * endScale;

            _playCoroutine = null;
            ClearMe(panelA, panelB);
            onComplete?.Invoke();
        }

        private void SetRadius(float radius)
        {
            material.SetFloat("_Radius", radius);
        }

        public void SetMaskPos(Vector3 localPos)
        {
            _imgMask.rectTransform.localPosition = localPos;
        }

        private void ClearMe(BasePanel a, BasePanel b)
        {
            var graphsA = a.GetComponentsInChildren<Graphic>();
            var graphsB = b.GetComponentsInChildren<Graphic>();

            foreach (var graph in graphsA)
                graph.material = null;

            foreach (var graph in graphsB)
                graph.material = null;
        }

        protected override void OnDestroy()
        {
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            base.OnDestroy();
        }
    }
}