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

    /// <summary>时段物理参数封装，环境光强度严格遵守暗部保底大于等于 0.35</summary>
    [Serializable]
    public struct TimeOfDaySetting
    {
        public TimeOfDayKind Kind;

        [Tooltip("全局主光颜色")]
        public Color LightColor;

        [Tooltip("全局光照强度，白天 0.75，黄昏 0.60，夜晚 0.45")]
        [Range(0.35f, 1.2f)]
        public float Intensity;

        [Tooltip("暗部环境补偿色，用于防止冷色调跌入死黑")]
        public Color AmbientFloorColor;

        public static TimeOfDaySetting GetDefault(TimeOfDayKind kind)
        {
            switch (kind)
            {
                case TimeOfDayKind.Dusk:
                    return new TimeOfDaySetting
                    {
                        Kind = TimeOfDayKind.Dusk,
                        LightColor = new Color(0.973f, 0.702f, 0.580f, 1f), // #F8B394 粉橘色
                        Intensity = 0.60f,
                        AmbientFloorColor = new Color(0.227f, 0.165f, 0.220f, 1f) // #3A2A38 深粉紫
                    };

                case TimeOfDayKind.Night:
                    return new TimeOfDaySetting
                    {
                        Kind = TimeOfDayKind.Night,
                        LightColor = new Color(0.478f, 0.459f, 0.627f, 1f), // #7A75A0 蓝紫色
                        Intensity = 0.45f,
                        AmbientFloorColor = new Color(0.133f, 0.125f, 0.208f, 1f) // #222035 深蓝灰
                    };

                default:
                    return new TimeOfDaySetting
                    {
                        Kind = TimeOfDayKind.Day,
                        LightColor = new Color(1f, 0.961f, 0.867f, 1f), // #FFF5DD 暖日光
                        Intensity = 0.75f,
                        AmbientFloorColor = new Color(0.35f, 0.35f, 0.35f, 1f)
                    };
            }
        }
    }
}
