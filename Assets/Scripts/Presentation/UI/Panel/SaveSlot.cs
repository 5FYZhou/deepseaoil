using System;
using System.Globalization;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeepseaOil.Presentation.UI
{
    public class SaveSlot : MonoBehaviour
    {
        private int _index;
        private bool _deleteListenerRegistered;

        [SerializeField] private TMP_Text txtTitle;
        [SerializeField] private TMP_Text txtTimeLeft;
        [SerializeField] private TMP_Text txtTimeRight;
        [SerializeField] private TMP_Text txtPlayerLeft;
        [SerializeField] private TMP_Text txtPlayerRight;
        [SerializeField] private TMP_Text txtProcessLeft;
        [SerializeField] private TMP_Text txtProcessRight;
        [SerializeField] private Button btnDelete;

        public void Init(int index)
        {
            _index = index;

            if (btnDelete == null)
            {
                Debug.LogError($"[Save] 存档槽 {_index} 未绑定删除按钮。", this);
                return;
            }

            if (_deleteListenerRegistered) return;

            btnDelete.onClick.AddListener(OnDeleteClicked);
            _deleteListenerRegistered = true;
        }

        private void OnDestroy()
        {
            if (!_deleteListenerRegistered || btnDelete == null) return;

            btnDelete.onClick.RemoveListener(OnDeleteClicked);
            _deleteListenerRegistered = false;
        }

        private void OnDeleteClicked()
        {
            EventBus<RequestDeleteSave>.Publish(new RequestDeleteSave(_index));
        }

        public void SetInfo(SaveSlotInfo info)
        {
            if (txtTitle != null) txtTitle.text = $"存档槽 {_index + 1}";
            if (txtPlayerLeft != null) txtPlayerLeft.text = string.Empty;
            if (txtPlayerRight != null) txtPlayerRight.text = string.Empty;
            if (txtProcessLeft != null) txtProcessLeft.text = "关卡进度";
            if (txtProcessRight != null) txtProcessRight.text = $"第 {info.CurrentLevelId} 关";

            if (DateTimeOffset.TryParseExact(
                info.SavedAtUtc,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset savedAtUtc))
            {
                DateTimeOffset localTime = savedAtUtc.ToLocalTime();
                if (txtTimeLeft != null)
                    txtTimeLeft.text = localTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                if (txtTimeRight != null)
                    txtTimeRight.text = localTime.ToString("HH:mm", CultureInfo.InvariantCulture);
                return;
            }

            if (txtTimeLeft != null) txtTimeLeft.text = "--";
            if (txtTimeRight != null) txtTimeRight.text = "--";
        }
    }
}
