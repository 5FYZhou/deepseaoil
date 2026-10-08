using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace DeepseaOil.Presentation.UI
{
    public abstract class BasePanel : MonoBehaviour
    {
        protected Dictionary<string, UIBehaviour> components = new();

        private readonly HashSet<string> _ignoreName = new()
    {
        "Text (TMP)","Button",
        "Image","RawImage","Background","Checkmark","Label",
        "Text (Legacy)","Arrow","Placeholder","Fill","Handle",
        "Viewport","Scrollbar Horizontal","Scrollbar Vertical"
    };

        public abstract E_UILayer Layer { get; }
        public abstract bool CanBeHideByKey { get; }

        protected virtual void Awake()
        {
            components.Clear();
            FindComponentsOnChildren();
        }

        public virtual void ShowMe()
        {

        }

        public virtual void HideMe()
        {

        }

        protected virtual void OnButtonClicked(string name)
        {
        }

        protected virtual void OnSliderValueChange(string sliderName, float value) { }

        /// <summary>只在 Awake 时被调用一次</summary>
        protected virtual string SetInitialTxt(string name)
        {
            return "Empty";
        }

        private void FindComponentsOnChildren()
        {
            var componentsInChildren = GetComponentsInChildren<UIBehaviour>(true);

            foreach (var component in componentsInChildren)
            {
                // 精确名字匹配：Unity 给重名子物体加序号后缀（Image (1)），带序号的漏网者是 Graphic，走下面 else if。
                if (_ignoreName.Contains(component.name))
                {
                    continue;
                }

                components[component.name] = component;

                if (component is Button button)
                {
                    string buttonName = component.name;

                    button.onClick.AddListener(() =>
                    {
                        OnButtonClicked(buttonName);
                    });
                }
                else if (component is TMP_Text text)
                {
                    text.text = SetInitialTxt(component.name);
                }
                else if (component is Graphic)
                {
                    // 纯图形不自动绑定也不告警，已注册进 components 字典可按名字取。必须静默：每个 Image 都走到这里（面板底图、层级父对象、按钮身上那张底图），实测 BeginPanel 一次刷 11 条告警。
                    // 告警只留给可能需要绑定而框架没处理的控件（Toggle / Slider / Dropdown / InputField / ScrollRect，它们不是 Graphic，会落到 else）。
                }
                else if (component is Slider slider)
                {
                    slider.onValueChanged.AddListener((value) =>
                    {
                        OnSliderValueChange(slider.name, value);
                    });
                }
                else
                {
                    Debug.LogWarning($"BasePanel:无{component.name}组件{component}的绑定操作");
                }
            }
        }

        protected T GetComponent<T>(string name) where T : UIBehaviour
        {
            if (components.TryGetValue(name, out var component))
            {
                return component as T;
            }

            Debug.LogWarning(
                $"[{GetType().Name}] 找不到 UI 组件：{name}"
            );

            return null;
        }
    }
}
