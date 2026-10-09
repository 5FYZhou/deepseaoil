using DeepseaOil.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
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

        // 存加载过的UI图片路径
        private readonly HashSet<string> _assetKeys = new();


        protected virtual void Awake()
        {
            components.Clear();
            FindComponentsOnChildren();
        }


        #region 基础行为
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
                    // 设置按钮样式
                    ApplyBtnStyle(button, SetBtnStyle(buttonName));
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
        #endregion

        #region UI样式

        /// <summary>
        /// 子类重写，子类控制自身的按钮样式
        /// </summary>
        protected virtual ButtonStyle SetBtnStyle(string name)
        {
            return ButtonStyle.None;
        }

        private void ApplyBtnStyle(Button button, ButtonStyle style)
        {
            // 不设置样式
            if (style == ButtonStyle.None) return;

            // 拿对应按钮样式的图片路径和尺寸
            var info = UIStylePath.GetBtnStylePath(style);
            // 设置 RectTransform 尺寸
            if (info.size != Vector2.zero)
            {
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.sizeDelta = info.size;
            }
            var sprites = LoadSpriteAtlas(info.path);
            Sprite normal = sprites.GetSprite(info.normalName);
            Sprite highlighted = sprites.GetSprite(info.highlightedName);
            Sprite pressed = sprites.GetSprite(info.pressedName);
            Sprite selected = sprites.GetSprite(info.selectedName);
            Sprite disabled = sprites.GetSprite(info.disabledName);

            if (button.targetGraphic is Image image)
                image.sprite = normal;
            
            // 设为图片模式，悬浮/按下时自动切换图片
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = button.spriteState;
            state.highlightedSprite = highlighted;
            state.pressedSprite = pressed;
            state.selectedSprite = selected;
            state.disabledSprite = disabled;

            button.spriteState = state;
        }

        protected SpriteAtlas LoadSpriteAtlas(string key)
        {
            SpriteAtlas sprite = AssetModule.Load<SpriteAtlas>(key);

            if (sprite != null)
                _assetKeys.Add(key);

            return sprite;
        }

        protected virtual void ReleaseAssets()
        {
            foreach (string key in _assetKeys)
                AssetModule.Release(key);

            _assetKeys.Clear();
        }

        protected virtual void OnDestroy()
        {
            ReleaseAssets();
        }
        #endregion
    }
}
