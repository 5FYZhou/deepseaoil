using System.Collections;
using System.Collections.Generic;
using DeepseaOil.Logic;
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

        private TMP_Text txtContext;

        // 每行之间的间隔（秒）
        [SerializeField] private float lineInterval = 0.5f;

        // 开始前的停留时间（秒）
        [SerializeField] private float startDelay = 0.1f;

        // 按行拆分的字幕内容，用 List 方便遍历
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
            "圖錙、芝麻、爵士",
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

        private Coroutine showCoroutine;

        protected override void Awake()
        {
            base.Awake();
            txtContext = GetComponent<TMP_Text>("TxtContext");
            Debug.Log($"[EndPanel] txtContext = {(txtContext == null ? "NULL" : txtContext.name)}, " +
                      $"active = {gameObject.activeInHierarchy}, " +
                      $"lineInterval = {lineInterval}");
        }
        private void Start()
        {
            StartCoroutine(DebugTest());
        }

        private IEnumerator DebugTest()
        {
            for (int i = 0; i < 5; i++)
            {
                Debug.Log($"[EndPanel] test {i}");
                yield return new WaitForSeconds(0.5f);
            }
        }
        public override void ShowMe()
        {
            if (showCoroutine != null)
                StopCoroutine(showCoroutine);

            showCoroutine = StartCoroutine(PlayCredits());
        }

        public override void HideMe()
        {
            StopShow();
        }

        private void StopShow()
        {
            if (showCoroutine != null)
            {
                StopCoroutine(showCoroutine);
                showCoroutine = null;
            }
        }

        private IEnumerator PlayCredits()
        {
            // 初始清空
            txtContext.text = string.Empty;
            txtContext.maxVisibleCharacters = int.MaxValue;

            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            // 逐行追加
            for (int i = 0; i < CreditLines.Length; i++)
            {
                if (i == 0)
                    txtContext.text = CreditLines[i];
                else
                    txtContext.text += "\n" + CreditLines[i];
                Debug.Log(txtContext.text);

                yield return new WaitForSeconds(lineInterval);
            }

            // 全部显示完后停留一会儿，再自动跳主菜单
            yield return new WaitForSeconds(1.5f);
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