using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DeepseaOil.Logic;
using DeepseaOil.Data;
using DeepseaOil.Presentation;

namespace DeepseaOil.Presentation.UI
{
    [RequireComponent(typeof(Slider))]
    public class SliderEndDragSfx : MonoBehaviour, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            GameRoot.Instance.Audio.EnqueueSfx(cfg.dso.AudioId.UIVolumeSlide);
        }
    }
}
