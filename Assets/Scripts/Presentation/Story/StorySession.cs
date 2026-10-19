using System;

namespace DeepseaOil.Presentation.Story
{
    /// <summary>跨场景存活的剧情会话态：重开意图与只播一次的段标记</summary>
    public sealed class StorySession
    {
        private string _restartSceneName;

        public bool SeedDialogueSeen { get; private set; }

        public void MarkSeedDialogueSeen()
        {
            SeedDialogueSeen = true;
        }

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
