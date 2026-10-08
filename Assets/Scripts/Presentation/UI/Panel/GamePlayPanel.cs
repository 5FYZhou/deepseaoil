using System.Collections;
using DeepseaOil.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class GamePlayPanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Bottom;
        public override bool CanBeHideByKey => false;

        [SerializeField] private RectTransform mask;

        // 斜遮罩 恰好跟两条血线吻合的位置
        // （写死了跟当前图片位置绑定，不要动图片）
        private int maskWidthWhen3 = 963;
        private int maskWidthWhen2 = 767;
        private int maskWidthWhen1 = 600;
        private int maskWidthWhen0 = 392;

        private float healthAnimDuration = 0.25f;

        private TMP_Text txtCountdown;
        private TMP_Text txtNextWave;

        private int curHealth;
        private int curMaskWidth;


        private Coroutine healthAnimCoroutine;

        protected override void Awake()
        {
            base.Awake();

            txtCountdown = GetComponent<TMP_Text>("TxtCountdown");
            txtNextWave = GetComponent<TMP_Text>("TxtNextWave");

            curHealth = 3;
            curMaskWidth = maskWidthWhen3;

            SetMaskWidth(curMaskWidth);
        }

        public override void ShowMe() { }

        public override void HideMe() { }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnPause":
                    GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    break;

                case "BtnSetting":
                    // GameRoot.Instance.Game.ChangeState(GameState.Paused);
                    break;
            }
        }

        /// <summary>
        /// 血量变化时的动画效果，暂定参数为 int newHealth
        /// </summary>
        private void OnHealthChanged(int newHealth)
        {
            // 限制血量范围
            newHealth = Mathf.Clamp(newHealth, 0, 3);

            // 没有变化就不用播放动画
            if (newHealth == curHealth)
                return;

            // 新的目标宽度
            int targetWidth = GetMaskWidth(newHealth);

            // 如果当前还有动画：
            // 先让它立即到达“旧动画的最终位置”
            if (healthAnimCoroutine != null)
            {
                StopCoroutine(healthAnimCoroutine);
                healthAnimCoroutine = null;

                SetMaskWidth(curMaskWidth);
            }

            // 记录新的目标状态
            int startWidth = curMaskWidth;

            curHealth = newHealth;

            // 开始新的动画
            healthAnimCoroutine = StartCoroutine(
                AnimateHealth(startWidth, targetWidth)
            );
        }

        private IEnumerator AnimateHealth(int startWidth, int targetWidth)
        {
            float elapsed = 0f;

            while (elapsed < healthAnimDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / healthAnimDuration);
                t = Mathf.SmoothStep(0f, 1f, t);

                curMaskWidth = Mathf.RoundToInt(
                    Mathf.Lerp(startWidth, targetWidth, t)
                );

                SetMaskWidth(curMaskWidth);

                yield return null;
            }

            // 确保最终位置完全准确
            curMaskWidth = targetWidth;
            SetMaskWidth(curMaskWidth);

            healthAnimCoroutine = null;
        }

        private void SetMaskWidth(int width)
        {
            Vector2 size = mask.sizeDelta;
            size.x = width;
            mask.sizeDelta = size;
        }

        private int GetMaskWidth(int health)
        {
            return health switch
            {
                3 => maskWidthWhen3,
                2 => maskWidthWhen2,
                1 => maskWidthWhen1,
                0 => maskWidthWhen0,
                _ => maskWidthWhen0
            };
        }
    }
}