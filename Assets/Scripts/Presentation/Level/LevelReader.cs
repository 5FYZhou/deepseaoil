using DeepseaOil.Data;
using DeepseaOil.Foundation;
using DeepseaOil.Logic.Level;
using System;
using System.Globalization;
using UnityEngine;

namespace DeepseaOil.Presentation.Level
{
    public sealed class LevelReader : MonoBehaviour, ILevelReader
    {
        private const string ScenePrefix = "Level_";

        private int _curLevelId;

        [SerializeField]
        [Tooltip("下一关卡id，对应level_wave表的id；小于等于0表示没有下一关")]
        private int _nextLevelId;

        [SerializeField]
        [Tooltip("波次模式：顺序/循环。循环无法进入胜利结算界面")]
        private WavePlayMode _wavePlayMode = WavePlayMode.Sequential;

        public int CurrentLevelId => _curLevelId;

        public int NextLevelId => _nextLevelId;

        public WavePlayMode PlayMode => _wavePlayMode;

        private void Awake()
        {
            string sceneName = gameObject.scene.name;

            if (!sceneName.StartsWith(ScenePrefix, StringComparison.Ordinal)
                || !int.TryParse(
                    sceneName.Substring(ScenePrefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _curLevelId))
            {
                throw new InvalidOperationException(
                    $"[Level] 场景名 '{sceneName}' 无法解析关卡 ID；关卡场景必须使用 'Level_非负整数' 命名（0 = 教学关）。");
            }
        }

    }
}
