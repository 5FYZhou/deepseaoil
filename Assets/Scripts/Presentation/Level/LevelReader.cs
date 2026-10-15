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

        [SerializeField]
        [ReadOnly]
        [Tooltip("当前关卡id，程序自动读")]
        private int _curLevelId;

        [SerializeField]
        [Tooltip("下一关卡id，整数，与level_wave表中的id对应")]
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
                    out _curLevelId)
                || _curLevelId <= 0)
            {
                throw new InvalidOperationException(
                    $"[Level] 场景名 '{sceneName}' 无法解析关卡 ID；关卡场景必须使用 'Level_正整数' 命名。");
            }
        }

    }
}
