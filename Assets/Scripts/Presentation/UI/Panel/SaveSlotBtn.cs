using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeepseaOil.Presentation.UI
{
    public class SaveSlotBtn : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // 
        private int idx;
        private string txtNum;
        private string txtTitle;
        private string txtTimeLeft;
        private string txtTimeRight;
        private string txtPlayerLeft;
        private string txtPlayerRight;
        private string txtProcessLeft;
        private string txtProcessRight;

        public void OnPointerEnter(PointerEventData eventData)
        {
            //targetChildImage.sprite = hoverSprite;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            //targetChildImage.sprite = normalSprite;
        }
        
        public void Init(int i)
        {
            idx = i;

        }
    }
}
