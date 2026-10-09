using System.Collections;
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
        Default
    }

    public readonly struct ButtonStyleInfo
    {
        public readonly string path;
        public readonly string normalName;
        public readonly string highlightedName;
        public readonly string pressedName;
        public readonly string selectedName;
        public readonly string disabledName;
        public readonly Vector2 size;

        public ButtonStyleInfo(string p, string normal, string highlighted, string pressed, string selected, string disabled, Vector2 s)
        {
            this.path = p;
            normalName = normal;
            highlightedName = highlighted;
            pressedName = pressed;
            selectedName = selected;
            disabledName = disabled;
            size = s;
        }
    }

    public static class UIStylePath
    {
        public static ButtonStyleInfo GetBtnStylePath(ButtonStyle style)
        {
            return style switch
            {
                ButtonStyle.Default => new ButtonStyleInfo(
                    "art/UI/ui_panel_tongyong",
                    "ui_panel_tongyong_0",
                    "ui_panel_tongyong_2",
                    "ui_panel_tongyong_3",
                    "ui_panel_tongyong_1",
                    "ui_panel_tongyong_0",
                    new Vector2(465, 112)),

                _ => new ButtonStyleInfo()
            };
        }
    }
}
