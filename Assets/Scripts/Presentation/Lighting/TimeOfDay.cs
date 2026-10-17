using System;
using UnityEngine;

namespace DeepseaOil.Presentation.Lighting
{
    /// <summary>全局时段枚举</summary>
    public enum TimeOfDayKind
    {
        Day = 0,
        Dusk = 1,
        Night = 2,
    }

    /// <summary>一个时段的全局主光外观：EnvironmentDirector 上三段各占一个字段，Inspector 直接改</summary>
    [Serializable]
    public struct TimeOfDaySetting
    {
        [Tooltip("全局主光颜色（线性）：夜景越偏蓝紫，火光越显暖")]
        public Color LightColor;

        [Tooltip("全局主光强度：与颜色乘完就是暗部基础亮度，压得越低火光反差越强")]
        [Range(0.15f, 1.2f)]
        public float Intensity;

        /// <summary>sRGB 十六进制 → 线性；只有白昼走这条，黄昏与夜晚按 Range 现值直接写三分量</summary>
        private static Color Srgb(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color parsed) ? parsed.linear : Color.white;
        }

        public static TimeOfDaySetting GetDefault(TimeOfDayKind kind)
        {
            switch (kind)
            {
                case TimeOfDayKind.Dusk:
                    return new TimeOfDaySetting
                    {
                        // Range 现值原样：拾色器给的是 #FF7040 分量，经 Srgb 转线性会把这层橙压暗一档
                        LightColor = new Color(1f, 0.4392157f, 0.2509804f, 1f),
                        Intensity = 0.75f
                    };

                case TimeOfDayKind.Night:
                    return new TimeOfDaySetting
                    {
                        LightColor = new Color(0f, 0f, 0.6784314f, 1f),
                        Intensity = 0.25f
                    };

                default:
                    return new TimeOfDaySetting
                    {
                        LightColor = Srgb("#FFFBF0"),
                        Intensity = 0.98f
                    };
            }
        }
    }
}
