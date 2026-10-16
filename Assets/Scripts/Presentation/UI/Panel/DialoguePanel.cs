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

        private Image _imgNPC;
        private TMP_Text _txtNPC;
        private Image _imgMachine;
        private TMP_Text _txtMachine;
        private Image _imgNPCPopup;
        private TMP_Text _txtNPCPopup;
        private Image _imgPlayerPopup;
        private TMP_Text _txtPlayerPopup;

        private bool _awaitingContinue;

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
                    break;
                case DialogType.Machine:
                    if (_imgMachine == null || _txtMachine == null)
                        return ReportMissingContent("终端对话框");

                    HideDialogueContent();
                    _txtMachine.text = line.Context;
                    _imgMachine.raycastTarget = true;
                    _imgMachine.gameObject.SetActive(true);
                    _txtMachine.gameObject.SetActive(true);
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

                    HidePopupContent();
                    _txtNPCPopup.text = line.Context;
                    _imgNPCPopup.gameObject.SetActive(true);
                    _txtNPCPopup.gameObject.SetActive(true);
                    return true;
                case DialogType.PlayerPopup:
                    if (_imgPlayerPopup == null || _txtPlayerPopup == null)
                        return ReportMissingContent("玩家弹窗");

                    HidePopupContent();
                    _txtPlayerPopup.text = line.Context;
                    _imgPlayerPopup.gameObject.SetActive(true);
                    _txtPlayerPopup.gameObject.SetActive(true);
                    return true;
                default:
                    Debug.LogError($"[DialoguePanel] 不能用弹窗显示类型 {line.Type}。", this);
                    return false;
            }
        }

        public void HideDialogueContent()
        {
            _awaitingContinue = false;
            SetVisible(_imgNPC, false);
            SetVisible(_txtNPC, false);
            SetVisible(_imgMachine, false);
            SetVisible(_txtMachine, false);
        }

        public void HidePopupContent()
        {
            SetVisible(_imgNPCPopup, false);
            SetVisible(_txtNPCPopup, false);
            SetVisible(_imgPlayerPopup, false);
            SetVisible(_txtPlayerPopup, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_awaitingContinue) return;

            _awaitingContinue = false;
            ContinueRequested?.Invoke();
        }

        private void HideAllContent()
        {
            HideDialogueContent();
            HidePopupContent();
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
            return TxtStyle.Dialogue;
        }

    }
}
