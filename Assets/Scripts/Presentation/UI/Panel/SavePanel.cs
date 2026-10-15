using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class SavePanel : BasePanel
    {
        public override E_UILayer Layer => E_UILayer.Top;
        public override bool CanBeHideByKey => true;
        public override bool CloseOnSceneTransition => true;

        [SerializeField]
        private GameObject saveInfoPre;

        private SavePanelMode _mode = SavePanelMode.Load;
        private readonly SaveSlot[] _slotViews = new SaveSlot[SaveSlotInfo.SlotCount];
        private readonly ButtonStyle[] _slotStyles = new ButtonStyle[SaveSlotInfo.SlotCount];
        private readonly Color[] _slotGraphicColors = new Color[SaveSlotInfo.SlotCount];
        private readonly bool[] _slotGraphicColorsCaptured = new bool[SaveSlotInfo.SlotCount];
        private SaveSlotInfo[] _slotInfos;
        private bool _subscribed;

        public void SetMode(SavePanelMode mode)
        {
            _mode = mode;
            RefreshButtons();
        }

        public override void ShowMe()
        {
            Subscribe();
            EventBus<RequestHudRefresh>.Publish(new RequestHudRefresh());
        }

        public override void HideMe()
        {
            Unsubscribe();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Unsubscribe();
        }

        protected override ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        protected override void OnButtonClicked(string name)
        {
            switch (name)
            {
                case "BtnClose":
                    GameRoot.Instance.UI.HidePanel<SavePanel>();
                    break;
                case "BtnSave0":
                case "BtnSave1":
                case "BtnSave2":
                case "BtnSave3":
                    HandleSlotClick(name);
                    break;
            }
        }

        private void HandleSlotClick(string buttonName)
        {
            int slotIndex = GetSlotIndex(buttonName);
            if (slotIndex < 0)
                return;

            if (_mode == SavePanelMode.Save)
            {
                EventBus<RequestSave>.Publish(
                    new RequestSave(slotIndex, new GameData(CombatRoot.Current != null
                        ? CombatRoot.Current.CurrentLevelId
                        : 0)));
                return;
            }

            EventBus<RequestLoad>.Publish(new RequestLoad(slotIndex));
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            _subscribed = true;
            EventBus<SaveSlotsRefreshed>.Subscribe(OnSaveSlotsRefreshed);
            EventBus<SaveLoaded>.Subscribe(OnSaveLoaded);
            EventBus<SaveResult>.Subscribe(OnSaveResult);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            _subscribed = false;
            EventBus<SaveSlotsRefreshed>.Unsubscribe(OnSaveSlotsRefreshed);
            EventBus<SaveLoaded>.Unsubscribe(OnSaveLoaded);
            EventBus<SaveResult>.Unsubscribe(OnSaveResult);
        }

        private void OnSaveSlotsRefreshed(SaveSlotsRefreshed evt)
        {
            if (evt.Slots == null || evt.Slots.Length != SaveSlotInfo.SlotCount)
            {
                Debug.LogError($"[Save] 存档服务返回的槽位数据无效，预期 {SaveSlotInfo.SlotCount} 个。");
                return;
            }

            _slotInfos = evt.Slots;
            RefreshButtons();
        }

        private void OnSaveLoaded(SaveLoaded evt)
        {
            string sceneName = SceneName.GetLevel(evt.Data.currentLevelId);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[Save] 存档关卡场景 '{sceneName}' 不存在或未加入 Build Settings，无法读取存档。");
                return;
            }

            GameRoot root = GameRoot.Instance;
            //root.UI.CloseAllPanels();
            //root.Game.ChangeState(GameState.Running);
            EventBus<RequestLoadScene>.Publish(new RequestLoadScene(sceneName, true));
        }

        private void OnSaveResult(SaveResult evt)
        {
            if (evt.Success) return;

            Debug.LogError($"[Save] 存档槽 {evt.SlotIndex} 操作失败：{evt.Error}");
        }

        private void RefreshButtons()
        {
            if (_slotInfos == null) return;

            if (!EnsureSlotViews()) return;

            for (int i = 0; i < SaveSlotInfo.SlotCount; i++)
            {
                Button button = GetComponent<Button>($"BtnSave{i}");
                if (button == null)
                    continue;

                SaveSlotInfo slot = _slotInfos[i];
                bool hasSave = slot.Status == SaveSlotStatus.Available;
                button.interactable = _mode == SavePanelMode.Save || hasSave;
                ButtonStyle style = hasSave ? ButtonStyle.SaveSlot : ButtonStyle.EmptySaveSlot;
                if (_slotStyles[i] != style)
                {
                    ApplyBtnStyle(button, style);
                    _slotStyles[i] = style;
                }

                _slotViews[i].gameObject.SetActive(hasSave);
                if (hasSave)
                    _slotViews[i].SetInfo(slot);
            }
        }

        private bool EnsureSlotViews()
        {
            if (saveInfoPre == null)
            {
                Debug.LogError("[Save] SavePanel 未接入 saveInfoPre 预制体。", this);
                return false;
            }

            SaveSlot template = saveInfoPre.GetComponent<SaveSlot>();
            if (template == null)
            {
                Debug.LogError("[Save] saveInfoPre 上缺少 SaveSlot 组件。", this);
                return false;
            }

            saveInfoPre.SetActive(false);

            for (int i = 0; i < SaveSlotInfo.SlotCount; i++)
            {
                if (_slotViews[i] != null) continue;

                Button button = GetComponent<Button>($"BtnSave{i}");
                if (button == null)
                    return false;

                if (i == 0)
                {
                    _slotViews[i] = template;
                }
                else
                {
                    GameObject slotObject = Instantiate(saveInfoPre, button.transform, false);
                    _slotViews[i] = slotObject.GetComponent<SaveSlot>();
                    if (_slotViews[i] == null)
                    {
                        Debug.LogError($"[Save] 存档槽 {i} 的存档信息预制体缺少 SaveSlot 组件。", slotObject);
                        return false;
                    }
                }

                _slotViews[i].transform.SetParent(button.transform, false);
                _slotViews[i].Init(i);
                _slotViews[i].gameObject.SetActive(false);
            }

            return true;
        }

        private static int GetSlotIndex(string buttonName)
        {
            switch (buttonName)
            {
                case "BtnSave0": return 0;
                case "BtnSave1": return 1;
                case "BtnSave2": return 2;
                case "BtnSave3": return 3;
                default: return -1;
            }
        }
    }
}
