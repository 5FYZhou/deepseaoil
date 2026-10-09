using DeepseaOil.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    public enum ButtonStyle
    {
        /// <summary>
        /// 代码不设置样式（在编辑器里设置）
        /// </summary>
        None = 0,
        /// <summary>
        /// 开始界面等
        /// </summary>
        Normal
    }

    [Serializable]
    public struct ButtonStyleInfo
    {
        public ButtonStyle buttonStyle;
        public string path;
        public string normalName;
        public string highlightedName;
        public string pressedName;
        public string selectedName;
        public string disabledName;
        public Vector2 size;
    }

    [CreateAssetMenu(fileName = "UIConfig", menuName = "DeepseaOil/Settings/UIConfig")]
    public class UIConfig : BaseConfig
    {
        public List<ButtonStyleInfo> buttonStyles;

        public bool TryGetBtnStylePath(ButtonStyle style, out ButtonStyleInfo styleInfo)
        {
            foreach (ButtonStyleInfo info in buttonStyles)
            {
                if(info.buttonStyle == style)
                {
                    styleInfo = info;
                    return true;
                }
            }
            styleInfo = default;
            return false;
        }
    }
}
