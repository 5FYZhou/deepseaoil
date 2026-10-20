using System;

namespace DeepseaOil.Presentation.Story
{
    /// <summary>跨场景存活的重开意图</summary>
    public sealed class StorySession
    {
        private string _restartSceneName;

        public void RequestRestartAtLevelStart(string sceneName)
        {
            _restartSceneName = sceneName;
        }

        public void ClearRestartAtLevelStart()
        {
            _restartSceneName = null;
        }

        public bool TryConsumeRestartAtLevelStart(string sceneName)
        {
            if (_restartSceneName == null) return false;

            string target = _restartSceneName;
            _restartSceneName = null;

            return string.Equals(target, sceneName, StringComparison.Ordinal);
        }
    }
}
