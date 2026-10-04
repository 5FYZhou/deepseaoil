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

        // 只读，子类必须赋值
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
            /*
             子类如
            switch(name){
            case "AAA":
                f();
            case "BBB":
                f2();

             */
        }

        protected virtual void OnSliderValueChange(string sliderName, float value) { }

        /// <summary>
        /// 默认只会在Awake时被调用一次
        /// </summary>
        protected virtual string SetInitialTxt(string name)
        {
            return "Empty";
        }

        private void FindComponentsOnChildren()
        {
            var componentsInChildren = GetComponentsInChildren<UIBehaviour>(true);

            foreach (var component in componentsInChildren)
            {
                // 排除默认名称。
                // 注意：这里是**精确名字**匹配，而 Unity 会给重名子物体自动加序号后缀
                // （Image / Image (1) / Image (2)…）。这些带序号的漏网者不会走到下面的告警分支
                // —— 因为它们都是 Graphic，见下面的 else if。
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
                    // 纯图形（Image / RawImage / 非 TMP 的 Text）不需要自动绑定，也不告警。
                    // 它已经注册进 components 字典，子类可以用 GetComponent<Image>("人") 按名字取。
                    //
                    // 为什么必须静默：面板上的每个 Image 都会走到这里 —— 包括面板根节点的底图、
                    // 层级父对象（Bottom/Top）、以及**按钮自己身上的那张底图**
                    // （按钮走 Button 分支绑定了点击，同一个物体上的 Image 仍会再进一次循环）。
                    // 实测 BeginPanel 一次实例化刷 11 条「无XXX组件的绑定操作」，全是这条。
                    //
                    // 保留告警的判据：只对**可能需要绑定、而框架没处理**的控件类型告警
                    // （Toggle / Slider / Dropdown / InputField / ScrollRect —— 它们不是 Graphic，仍会落到 else）。
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
