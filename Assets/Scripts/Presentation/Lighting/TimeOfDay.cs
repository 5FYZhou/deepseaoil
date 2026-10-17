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

        /// <summary>把 sRGB 十六进制转成线性空间颜色；工程是 Linear，直接 new Color(hex分量) 会被当成线性值而整体偏暗偏灰</summary>
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
                        LightColor = Srgb("#FFB066"),
                        Intensity = 0.78f
                    };

                case TimeOfDayKind.Night:
                    return new TimeOfDaySetting
                    {
                        LightColor = Srgb("#8C88F1"),
                        Intensity = 0.30f
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
