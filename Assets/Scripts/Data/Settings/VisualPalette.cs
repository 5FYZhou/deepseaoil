﻿using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Data.Settings
{
    [Serializable]
    public struct BallColorEntry
    {
        public BallType Type;

        public Color Color;
    }

    [Serializable]
    public struct DropColorEntry
    {
        public DropType Type;

        public Color Color;
    }

    [CreateAssetMenu(fileName = "VisualPalette", menuName = "DeepseaOil/Settings/VisualPalette")]
    public sealed class VisualPalette : ScriptableObject
    {
        public const string ResourceKey = "tuning/VisualPalette";

        [Header("球种色（每个 BallType 一行）")]
        [Tooltip("加新球种时在此加一行；未列出的球种回落到 unknownBall 并警告一次")]
        public BallColorEntry[] ballColors = new BallColorEntry[0];

        // 迁移字段：首次打开本资产时迁往 ballColors，勿删
        [HideInInspector]
        public Color waterBall = new(0.20f, 0.55f, 1.00f, 1f);

        [HideInInspector]
        public Color earthBall = new(0.55f, 0.36f, 0.18f, 1f);

        [Tooltip("未知球种的兜底色（不会看不见，也不会误导成某一颗球）")]
        public Color unknownBall = Color.white;

        [Header("掉落物色（每个 DropType 一行）")]
        [Tooltip("加新掉落物时在此加一行；未列出的回落到 unknownBall 并警告一次")]
        public DropColorEntry[] dropColors = new DropColorEntry[0];

        [Header("敌人身体 · 两态")]
        [Tooltip("正常态：真身渲染器的颜色乘数。白色＝美术原色不变；美术有自带配色时改这里即可，不用动预制体")]
        public Color enemyBodyNormal = Color.white;

        [Tooltip("踩在减速格里：整体压暗（色相不变）；真身是彩色美术时不要用有色乘数，否则连红眼一起改色")]
        public Color enemyBodySlowed = new(0.45f, 0.45f, 0.45f, 1f);

        [Header("瞄准高亮 · 两态")]
        [Tooltip("可投时（白，半透明：它是提示不是物体）")]
        public Color highlightAvailable = new(1f, 1f, 1f, 0.32f);

        [Tooltip("不可投时（红）")]
        public Color highlightBlocked = new(1f, 0.25f, 0.2f, 0.42f);

        [Header("贴地件")]
        [Tooltip("球阴影色：存在感来自它，不走球种色（阴影是光不是材质）")]
        public Color shadow = new(0f, 0f, 0f, 0.35f);

        [Header("伤害飘字 · 元素色")]
        public Color damagePhysical = Color.white;

        public Color damageFire = new(1.00f, 0.55f, 0.20f, 1f);

        public Color damageElectric = new(0.55f, 0.85f, 1.00f, 1f);

        public Color damageIce = new(0.70f, 0.90f, 1.00f, 1f);

        [Header("HUD 受击")]
        [Tooltip("血条受击溅射粒子色：与 LIFE 条暖橙同族，别用纯白（会在奶油底上糊成一片）")]
        public Color hudHitSpark = new(1f, 0.72f, 0.42f, 1f);

        /// <summary>取观感颜色表；缺失时返回字段默认值实例</summary>
        internal static VisualPalette LoadOrDefault()
        {
            if (Cached != null) return Cached;

            if (AssetModule.IsInitialized)
            {
                VisualPalette loaded = AssetModule.Load<VisualPalette>(ResourceKey);

                if (loaded != null)
                {
                    Cached = loaded;

                    return loaded;
                }
            }

            Debug.LogWarning(
                $"[Tuning] 取不到 {ResourceKey}（未接线或资产不存在），改用代码默认值。观感颜色将在本局退回白模那一套。");

            Cached = CreateInstance<VisualPalette>();

            return Cached;
        }

        private static VisualPalette Cached;

        // 只在覆盖位图变化时才报告：OnValidate 频繁，逐次报会刷屏
        private int _reportedBallColors = -1;

        private int _reportedDropColors = -1;

        public Color BallColor(BallType type)
        {
            if (ballColors != null)
            {
                for (int i = 0; i < ballColors.Length; i++)
                {
                    if (ballColors[i].Type == type) return ballColors[i].Color;
                }
            }

            return unknownBall;
        }

        /// <summary>掉落物与球同色（掉落物就是材料本身）；两处数组独立填</summary>
        public Color DropColor(DropType type)
        {
            if (dropColors != null)
            {
                for (int i = 0; i < dropColors.Length; i++)
                {
                    if (dropColors[i].Type == type) return dropColors[i].Color;
                }
            }

            return unknownBall;
        }

        /// <summary>飘字配色：int 是 ElementKind 的序号（0/1/2/3），刻意不引 Logic 以免 Data→Logic 反向依赖</summary>
        public Color DamageColor(int element)
        {
            switch (element)
            {
                case 1:
                    return damageFire;

                case 2:
                    return damageElectric;

                case 3:
                    return damageIce;

                default:
                    return damagePhysical;
            }
        }

        // 减速压暗：0.7 = 表里泥浆档，到它吃满 enemyBodySlowed
        public Color SlowTintColor(float speedScale)
        {
            const float fullScale = 0.7f;

            return Color.Lerp(Color.white, enemyBodySlowed, Mathf.Clamp01((1f - speedScale) / (1f - fullScale)));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            MigrateLegacyColors();

            ValidateEntries();
        }

        private void MigrateLegacyColors()
        {
            if (ballColors != null && ballColors.Length > 0) return;

            List<BallColorEntry> migrated = new List<BallColorEntry>(2);

            // default(Color) 视为没填：纯透明黑会被误判
            if (waterBall != default(Color)) migrated.Add(new BallColorEntry { Type = BallType.Water, Color = waterBall });

            if (earthBall != default(Color)) migrated.Add(new BallColorEntry { Type = BallType.Earth, Color = earthBall });

            if (migrated.Count == 0) return;

            ballColors = migrated.ToArray();

            waterBall = default;
            earthBall = default;

            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log($"[VisualPalette] 已迁移 waterBall / earthBall 到 ballColors 数组（{migrated.Count} 条）。");
        }
#endif

        private void ValidateEntries()
        {
            ValidateBallColors();

            ValidateDropColors();
        }

        private void ValidateBallColors()
        {
            int coverage = 0;
            bool duplicated = false;

            if (ballColors != null)
            {
                for (int i = 0; i < ballColors.Length; i++)
                {
                    int bit = 1 << (int)ballColors[i].Type;

                    if ((coverage & bit) != 0) duplicated = true;

                    coverage |= bit;
                }
            }

            int signature = (coverage << 1) | (duplicated ? 1 : 0);

            if (signature == _reportedBallColors) return;

            _reportedBallColors = signature;

            if (duplicated)
            {
                for (int i = 0; i < ballColors.Length; i++)
                {
                    for (int j = i + 1; j < ballColors.Length; j++)
                    {
                        if (ballColors[i].Type != ballColors[j].Type) continue;

                        Debug.LogError(
                            $"[VisualPalette] ballColors 里 {ballColors[i].Type} 出现了两次（第 {i} 行与第 {j} 行）：只会取第一条。", this);
                    }
                }
            }

            string missing = MissingNames(typeof(BallType), coverage);

            if (missing.Length > 0)
            {
                Debug.LogWarning($"[VisualPalette] ballColors 未覆盖：{missing}，运行时回落到 unknownBall。", this);
            }
        }

        private void ValidateDropColors()
        {
            int coverage = 0;
            bool duplicated = false;

            if (dropColors != null)
            {
                for (int i = 0; i < dropColors.Length; i++)
                {
                    int bit = 1 << (int)dropColors[i].Type;

                    if ((coverage & bit) != 0) duplicated = true;

                    coverage |= bit;
                }
            }

            int signature = (coverage << 1) | (duplicated ? 1 : 0);

            if (signature == _reportedDropColors) return;

            _reportedDropColors = signature;

            if (duplicated)
            {
                for (int i = 0; i < dropColors.Length; i++)
                {
                    for (int j = i + 1; j < dropColors.Length; j++)
                    {
                        if (dropColors[i].Type != dropColors[j].Type) continue;

                        Debug.LogError(
                            $"[VisualPalette] dropColors 里 {dropColors[i].Type} 出现了两次（第 {i} 行与第 {j} 行）：只会取第一条。", this);
                    }
                }
            }

            string missing = MissingNames(typeof(DropType), coverage);

            if (missing.Length > 0)
            {
                Debug.LogWarning($"[VisualPalette] dropColors 未覆盖：{missing}，运行时回落到 unknownBall。", this);
            }
        }

        private static string MissingNames(Type enumType, int coverage)
        {
            List<string> missing = new List<string>();

            foreach (object value in Enum.GetValues(enumType))
            {
                if ((coverage & (1 << (int)value)) != 0) continue;

                missing.Add(value.ToString());
            }

            return string.Join(", ", missing);
        }
    }
}
