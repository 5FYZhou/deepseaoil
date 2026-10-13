using DeepseaOil.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Data
{
    public enum ButtonStyle
    {
        /// 代码不设置样式（在编辑器里设置）
        None = 0,
        /// 开始界面等
        /// </summary>
        Normal,
        /// <summary>
        /// 空存档按钮
        /// </summary>
        EmptySaveSlot,
        /// <summary>
        /// 有存档的存档按钮
        /// </summary>
        SaveSlot
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

    public enum TxtStyle
    {
        None = 0,
        Title,
        Body
    }

    [Serializable]
    public struct FontStyleInfo
    {
        public string path;
        public string name;
        public int size;
        public TxtStyle style;
        public FontStyle fontStyle;
    }

    [CreateAssetMenu(fileName = "UIConfig", menuName = "DeepseaOil/Settings/UIConfig")]
    public class UIConfig : BaseConfig
    {
        public List<ButtonStyleInfo> buttonStyles;
        public List<FontStyleInfo> fontStyles;

        public bool TryGetBtnStyleInfo(ButtonStyle style, out ButtonStyleInfo styleInfo)
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

        public bool TryGetFontStyleInfo(TxtStyle style, out FontStyleInfo styleInfo)
        {
            if (fontStyles != null)
            {
                foreach (FontStyleInfo info in fontStyles)
                {
                    if (info.style == style)
                    {
                        styleInfo = info;
                        return true;
                    }
                }
            }

            styleInfo = default;
            return false;
        }
    }
}
