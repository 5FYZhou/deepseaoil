using System.Collections;
using TMPro;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Presentation.UI
{
    public class EndPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.System;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        [SerializeField] private RectTransform viewport;
        [SerializeField] private float scrollSpeed = 60f;
        [SerializeField] private float startDelay = 0.1f;
        [SerializeField] private float endHoldSeconds = 1.5f;
        private static readonly string[] CreditLines =
                {
            "【深海鱼油工作室】",
            "策划",
            "代码鱿鱼、秋墨",
            "",
            "文案",
            "代码鱿鱼",
            "",
            "程序",
            "枫叶舟、V.Reason-愚人舟",
            "",
            "美术",
            "Banth、芝麻王、爵士JAZZEL",
            "",
            "运营",
            "鹦鹉螺",
            "",
            "【特别鸣谢】",
            "音乐 / 音效",
            "李tingke",
            "",
            "美术",
            "既灵",
        };

        private TMP_Text txtContext;
        private RectTransform txtRect;
        private Coroutine showCoroutine;

        protected override void Awake()
        {
            base.Awake();

            txtContext = GetComponent<TMP_Text>("TxtContext");
            if (txtContext == null)
                return;

            txtRect = txtContext.rectTransform;
            if (viewport == null)
                viewport = txtRect.parent as RectTransform;
        }

        public override void ShowMe()
        {
            StopShow();
            GameRoot.Instance.Audio.EnqueueBgm(cfg.dso.AudioId.BgmAscensio);
            showCoroutine = StartCoroutine(PlayCredits());
        }

        public override void HideMe()
        {
            StopShow();
        }

        private void StopShow()
        {
            if (showCoroutine == null)
                return;

            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }

        private IEnumerator PlayCredits()
        {
            if (txtContext == null || txtRect == null || viewport == null)
            {
                showCoroutine = null;
                yield break;
            }

            txtContext.text = string.Join("\n", CreditLines);
            txtContext.ForceMeshUpdate();

            float textHeight = txtContext.GetPreferredValues().y;
            Vector2 size = txtRect.sizeDelta;
            size.y = textHeight;
            txtRect.sizeDelta = size;

            float halfTravel = (viewport.rect.height + textHeight) * 0.5f;
            float speed = Mathf.Max(1f, scrollSpeed);

            Vector2 anchored = txtRect.anchoredPosition;
            anchored.y = -halfTravel;
            txtRect.anchoredPosition = anchored;

            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);

            while (anchored.y < halfTravel)
            {
                anchored.y += speed * Time.unscaledDeltaTime;
                txtRect.anchoredPosition = anchored;
                yield return null;
            }

            anchored.y = halfTravel;
            txtRect.anchoredPosition = anchored;

            if (endHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(endHoldSeconds);

            GoToBegin();

            showCoroutine = null;
        }

        private void GoToBegin()
        {
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(SceneName.Begin))
            {
                UnityEngine.Debug.LogError(
                    $"[Level] 主菜单场景 '{SceneName.Begin}' 不存在或未加入 Build Settings。",
                    this);
                return;
            }

            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(SceneName.Begin, true));
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtContext":
                    return TxtStyle.Body;
                default:
                    return TxtStyle.None;
            }
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnSkip":
                    GoToBegin();
                    break;
            }
        }

        protected override void OnDestroy()
        {
            StopShow();
            base.OnDestroy();
        }
    }
}
