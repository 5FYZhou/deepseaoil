using System;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using cfg.dso;

namespace DeepseaOil.Presentation.UI
{
    public class DialoguePanel : BasePanel, IPointerClickHandler
    {
        public override E_UILayer Layer => E_UILayer.Middle;
        public override bool CanBeHideByKey => false;
        public override bool CloseOnSceneTransition => true;

        public event Action ContinueRequested;
        public event Action PopupTypingCompleted;

        [SerializeField] private float charsPerSecond = 15f;
        [SerializeField] private float popupFadeDuration = 0.22f;
        [SerializeField] private float popupSlideEnterDuration = 0.26f;
        [SerializeField] private float popupSlideExitDuration = 0.22f;
        [SerializeField] private float popupEdgeMargin = 48f;
        [SerializeField] private float popupWobbleAngle = 0.75f;
        [SerializeField] private float popupWobbleHertz = 1.1f;

        private Image _imgNPC;
        private TMP_Text _txtNPC;
        private Image _imgMachine;
        private TMP_Text _txtMachine;
        private Image _imgNPCPopup;
        private TMP_Text _txtNPCPopup;
        private Image _imgPlayerPopup;
        private TMP_Text _txtPlayerPopup;

        private Typewriter _typewriter;
        private Typewriter _popupTypewriterNPC;
        private Typewriter _popupTypewriterPlayer;
        private PopupMotion _motionNPCPopup;
        private PopupMotion _motionPlayerPopup;
        private UIFader _faderNPCPopup;
        private UIFader _faderPlayerPopup;
        private bool _awaitingContinue;
        private bool _paused;

        public override void ShowMe()
        {
            HideAllContent();
        }

        public override void HideMe()
        {
            HideAllContent();
        }

        protected override void Awake()
        {
            base.Awake();
            _txtNPC = GetComponent<TMP_Text>("TxtNPC");
            _txtMachine = GetComponent<TMP_Text>("TxtMachine");
            _txtNPCPopup = GetComponent<TMP_Text>("TxtNPCPopup");
            _txtPlayerPopup = GetComponent<TMP_Text>("TxtPlayerPopup");
            _imgNPC = GetComponent<Image>("ImgNPC");
            _imgMachine = GetComponent<Image>("ImgMachine");
            _imgNPCPopup = GetComponent<Image>("ImgNPCPopup");
            _imgPlayerPopup = GetComponent<Image>("ImgPlayerPopup");
            ConfigurePopupImage(_imgNPCPopup);
            ConfigurePopupImage(_imgPlayerPopup);
            ConfigurePopupText(_txtNPCPopup);
            ConfigurePopupText(_txtPlayerPopup);
            _typewriter = new Typewriter(this);
            _popupTypewriterNPC = new Typewriter(this);
            _popupTypewriterPlayer = new Typewriter(this);
            _motionNPCPopup = EnsurePopupMotion(_imgNPCPopup);
            _motionPlayerPopup = EnsurePopupMotion(_imgPlayerPopup);
            _faderNPCPopup = UIFader.Create(this, _imgNPCPopup != null ? _imgNPCPopup.gameObject : null);
            _faderPlayerPopup = UIFader.Create(this, _imgPlayerPopup != null ? _imgPlayerPopup.gameObject : null);
            HideAllContent();
        }

        public bool ShowDialogue(DialogueSpec line)
        {
            switch (line.Type)
            {
                case DialogType.Npc:
                    if (_imgNPC == null || _txtNPC == null)
                        return ReportMissingContent("絮对话框");

                    HideDialogueContent();
                    _txtNPC.text = line.Context;
                    _imgNPC.raycastTarget = true;
                    _imgNPC.gameObject.SetActive(true);
                    _txtNPC.gameObject.SetActive(true);
                    _typewriter.Play(_txtNPC, charsPerSecond, PlayUiBlip);
                    break;
                case DialogType.Machine:
                    if (_imgMachine == null || _txtMachine == null)
                        return ReportMissingContent("终端对话框");

                    HideDialogueContent();
                    _txtMachine.text = line.Context;
                    _imgMachine.raycastTarget = true;
                    _imgMachine.gameObject.SetActive(true);
                    _txtMachine.gameObject.SetActive(true);
                    _typewriter.Play(_txtMachine, charsPerSecond, PlayUiBlip);
                    break;
                default:
                    Debug.LogError($"[DialoguePanel] 不能用对话框显示类型 {line.Type}。", this);
                    return false;
            }

            _awaitingContinue = true;
            return true;
        }

        public bool ShowPopup(DialogueSpec line)
        {
            switch (line.Type)
            {
                case DialogType.NpcPopup:
                    if (_imgNPCPopup == null || _txtNPCPopup == null)
                        return ReportMissingContent("絮弹窗");

                    HidePopupImmediate();
                    _txtNPCPopup.text = line.Context;
                    RevealPopup(
                        _imgNPCPopup,
                        _faderNPCPopup,
                        _motionNPCPopup,
                        _popupTypewriterNPC,
                        _txtNPCPopup,
                        0f);
                    return true;
                case DialogType.PlayerPopup:
                    if (_imgPlayerPopup == null || _txtPlayerPopup == null)
                        return ReportMissingContent("玩家弹窗");

                    HidePopupImmediate();
                    _txtPlayerPopup.text = line.Context;
                    RevealPopup(
                        _imgPlayerPopup,
                        _faderPlayerPopup,
                        _motionPlayerPopup,
                        _popupTypewriterPlayer,
                        _txtPlayerPopup,
                        -4f);
                    return true;
                default:
                    Debug.LogError($"[DialoguePanel] 不能用弹窗显示类型 {line.Type}。", this);
                    return false;
            }
        }

        public void HideDialogueContent()
        {
            _awaitingContinue = false;
            _typewriter.Stop();
            StopUiSfx();
            SetVisible(_imgNPC, false);
            SetVisible(_txtNPC, false);
            SetVisible(_imgMachine, false);
            SetVisible(_txtMachine, false);
        }

        public void HidePopupContent()
        {
            _popupTypewriterNPC.Stop();
            _popupTypewriterPlayer.Stop();
            StopUiSfx();
            _motionNPCPopup?.PlayExit();
            _motionPlayerPopup?.PlayExit();
            float fadeOutDuration = Mathf.Max(popupFadeDuration, popupSlideExitDuration);
            _faderNPCPopup?.FadeOut(fadeOutDuration);
            _faderPlayerPopup?.FadeOut(fadeOutDuration);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _typewriter.Paused = paused;
            _popupTypewriterNPC.Paused = paused;
            _popupTypewriterPlayer.Paused = paused;
            _motionNPCPopup.Paused = paused;
            _motionPlayerPopup.Paused = paused;

            if (paused)
                StopUiSfx();
        }

        public bool IsPopupTyping =>
            _popupTypewriterNPC.IsPlaying || _popupTypewriterPlayer.IsPlaying;

        public bool IsPopupVisible =>
            (_faderNPCPopup != null && _faderNPCPopup.IsVisible)
            || (_faderPlayerPopup != null && _faderPlayerPopup.IsVisible);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_paused) return;

            if (_typewriter.Complete())
            {
                StopUiSfx();
                return;
            }

            if (!_awaitingContinue) return;

            _awaitingContinue = false;
            ContinueRequested?.Invoke();
        }

        private void RevealPopup(
            Image image,
            UIFader fader,
            PopupMotion motion,
            Typewriter typewriter,
            TMP_Text text,
            float verticalBias)
        {
            SetVisible(image, true);
            SetVisible(text, true);
            FitPopupFrame(image, text, verticalBias);
            AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
            audio?.PlayUiSlide(true);
            fader?.FadeIn(Mathf.Max(popupFadeDuration, popupSlideEnterDuration));
            motion?.PlayEnter();
            typewriter.Play(text, charsPerSecond, PlayUiBlip, OnPopupTypingCompleted);
        }

        private void HideAllContent()
        {
            HideDialogueContent();
            HidePopupImmediate();
        }

        private void HidePopupImmediate()
        {
            _popupTypewriterNPC.Stop();
            _popupTypewriterPlayer.Stop();
            _motionNPCPopup?.HideImmediate();
            _motionPlayerPopup?.HideImmediate();
            _faderNPCPopup?.HideImmediate();
            _faderPlayerPopup?.HideImmediate();
        }

        private PopupMotion EnsurePopupMotion(Image target)
        {
            if (target == null)
                return null;

            PopupMotion motion = target.GetComponent<PopupMotion>();

            if (motion == null)
                motion = target.gameObject.AddComponent<PopupMotion>();

            motion.Configure(
                popupSlideEnterDuration,
                popupSlideExitDuration,
                popupEdgeMargin,
                popupWobbleAngle,
                popupWobbleHertz);

            return motion;
        }

        private void PlayUiBlip()
        {
            AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
            audio?.PlayUiBlip();
        }

        private void OnPopupTypingCompleted()
        {
            PopupTypingCompleted?.Invoke();
        }

        private static void ConfigurePopupImage(Image image)
        {
            if (image == null)
                return;

            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.fillCenter = true;
        }

        private static void ConfigurePopupText(TMP_Text text)
        {
            if (text == null)
                return;

            text.enableAutoSizing = true;
            text.fontSizeMin = 23f;
            text.fontSizeMax = 32.2f;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
        }

        private static void FitPopupFrame(Image image, TMP_Text text, float verticalBias)
        {
            if (image == null || image.rectTransform == null || text == null)
                return;

            text.enableAutoSizing = false;
            text.fontSize = text.fontSizeMax;
            text.ForceMeshUpdate();

            float preferredHeight = text.preferredHeight;
            Vector4 border = image.sprite != null ? image.sprite.border : Vector4.zero;
            float fixedVertical = border.y + border.w;
            float centerHeight = Mathf.Max(52f, preferredHeight + 16f);
            float targetHeight = Mathf.Ceil(fixedVertical + centerHeight);
            RectTransform popupRect = image.rectTransform;
            Vector2 size = popupRect.sizeDelta;
            popupRect.sizeDelta = new Vector2(size.x, targetHeight);

            RectTransform textRect = text.rectTransform;
            Vector2 textPosition = textRect.anchoredPosition;
            textPosition.y = (border.y - border.w - targetHeight + textRect.sizeDelta.y) * 0.5f
                + verticalBias;
            textRect.anchoredPosition = textPosition;

            text.enableAutoSizing = preferredHeight > textRect.rect.height;
            text.ForceMeshUpdate();
        }

        private void StopUiSfx()
        {
            AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
            audio?.StopUiSfx();
        }

        private bool ReportMissingContent(string content)
        {
            Debug.LogError($"[DialoguePanel] 面板缺少 {content} 对应的图片或文本组件。", this);
            return false;
        }

        private static void SetVisible(Behaviour component, bool visible)
        {
            if (component != null)
                component.gameObject.SetActive(visible);
        }

        protected override TxtStyle SetTxtStyle(string name)
        {
            switch (name)
            {
                case "TxtNPC":
                case "TxtMachine":
                    return TxtStyle.Dialogue1;
                case "TxtNPCPopup":
                case "TxtPlayerPopup":
                    return TxtStyle.Dialogue2;
            }
            return TxtStyle.Dialogue1;
        }

    }
}
