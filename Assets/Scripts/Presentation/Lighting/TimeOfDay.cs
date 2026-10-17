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

    /// <summary>时段物理参数封装：白天通透，夜晚压到 0.20 给网格火光留出明暗反差</summary>
    [Serializable]
    public struct TimeOfDaySetting
    {
        [Tooltip("时段标识：EnvironmentDirector 的覆盖数组按它匹配；重复 Kind 只取第一个")]
        public TimeOfDayKind Kind;

        [Tooltip("全局主光颜色；夜景越偏蓝紫，火光越显暖")]
        public Color LightColor;

        [Tooltip("全局光照强度，白天 0.98，黄昏 0.62，夜晚 0.20；压得越低火光反差越强")]
        [Range(0.15f, 1.2f)]
        public float Intensity;

        [Tooltip("暗部环境补偿色：防冷色调跌入死黑；当前未被 EnvironmentDirector 消费，改了不影响画面")]
        public Color AmbientFloorColor;

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
                        Kind = TimeOfDayKind.Dusk,
                        LightColor = Srgb("#F8B394"),
                        Intensity = 0.62f,
                        AmbientFloorColor = Srgb("#3A2A38")
                    };

                case TimeOfDayKind.Night:
                    return new TimeOfDaySetting
                    {
                        Kind = TimeOfDayKind.Night,
                        LightColor = Srgb("#7A75A0"),
                        Intensity = 0.20f,
                        AmbientFloorColor = Srgb("#12101F")
                    };

                default:
                    return new TimeOfDaySetting
                    {
                        Kind = TimeOfDayKind.Day,
                        LightColor = Srgb("#FFFBF0"),
                        Intensity = 0.98f,
                        AmbientFloorColor = Srgb("#595959")
                    };
            }
        }
    }
}
