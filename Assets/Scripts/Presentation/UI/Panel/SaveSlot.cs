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
        private bool _thumbnailWarned;

        [SerializeField] private TMP_Text txtTitle;
        [SerializeField] private TMP_Text txtTimeLeft;
        [SerializeField] private TMP_Text txtTimeRight;
        [SerializeField] private TMP_Text txtPlayerLeft;
        [SerializeField] private TMP_Text txtPlayerRight;
        [SerializeField] private TMP_Text txtProcessLeft;
        [SerializeField] private TMP_Text txtProcessRight;
        [SerializeField] private Button btnDelete;

        [SerializeField] private Image imgThumbnail;

        public void Init(int index)
        {
            _index = index;

            if (btnDelete == null)
            {
                Debug.LogError($"[Save] 存档槽 {_index} 未绑定删除按钮。", this);
                return;
            }

            btnDelete.gameObject.SetActive(_index != 0);

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

        public void SetThumbnail(Sprite sprite)
        {
            if (imgThumbnail == null)
            {
                if (_thumbnailWarned) return;

                _thumbnailWarned = true;

                Debug.LogWarning(
                    $"[Save] 存档槽 {_index + 1} 的 SaveSlot 未接线 imgThumbnail，缩略图不会显示。" +
                    "请在 SaveInfoSlot.prefab 里把缩略图节点拖到该字段。", this);

                return;
            }

            if (sprite == null) return;

            imgThumbnail.sprite = sprite;
        }

        public void SetInfo(SaveSlotInfo info)
        {
            if (txtTitle != null)
            {
                txtTitle.text = !string.IsNullOrEmpty(info.Title)
                    ? info.Title
                    : (_index > 0 ? $"存档槽 {_index + 1}" : "自动存档");
            }

            if (txtPlayerLeft != null) txtPlayerLeft.text = string.Empty;
            if (txtPlayerRight != null) txtPlayerRight.text = string.Empty;
            if (txtProcessLeft != null) txtProcessLeft.text = "关卡进度";
            if (txtProcessRight != null) txtProcessRight.text = $"第 {info.CurrentLevelId} 关";

            int split = string.IsNullOrEmpty(info.TimeText) ? -1 : info.TimeText.IndexOf(' ');

            if (txtTimeLeft != null)
                txtTimeLeft.text = split > 0 ? info.TimeText.Substring(0, split) : "--";

            if (txtTimeRight != null)
                txtTimeRight.text = split > 0 ? info.TimeText.Substring(split + 1) : "--";
        }
    }
}
