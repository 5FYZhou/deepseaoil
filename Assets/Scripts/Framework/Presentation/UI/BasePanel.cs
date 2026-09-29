using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace DeepSeaOil.Presentation
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

        protected virtual string OnTextChange(string name)
        {
            return "Empty";
        }

        private void FindComponentsOnChildren()
        {
            var componentsInChildren = GetComponentsInChildren<UIBehaviour>(true);

            foreach (var component in componentsInChildren)
            {
                // 排除默认名称
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
                    text.text = OnTextChange(component.name);
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
