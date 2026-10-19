using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>游戏状态机：切状态连带切面板、发暂停/恢复意图，是状态唯一入口。构造点只有 GameRoot.Assemble；面板操作走注入的 UIMgr，本类不认识 GameRoot。UI 输入只能经 IGameStateRequest 请求切状态。</summary>
    public sealed class GameManager : IGameStateRequest
    {
        public GameState CurState { get; private set; } = GameState.None;

        private GameState _stateBeforePause;
        private bool _hasStateBeforePause;
        private GameState _stateBeforeSceneTransition;
        private GameState _sceneTransitionTargetState;
        private bool _hasSceneTransitionTarget;
        private bool _sceneTransitionFadeFinished;
        private bool _sceneTransitionFailed;
        private bool _isFinalizingSceneTransition;

        public GameManager()
        {
        }

        public void RequestState(GameState state)
        {
            ChangeState(state);
        }

        public void ChangeState(GameState newState)
        {
            if (CurState == newState) return;
            if (CurState == GameState.SceneTransition && !_isFinalizingSceneTransition)
            {
                Debug.LogError($"[Game] 场景转场期间拒绝直接切换到状态 {newState}。");
                return;
            }

            if (newState == GameState.Paused)
            {
                _stateBeforePause = CurState;
                _hasStateBeforePause = true;
            }

            switch (newState)
            {
                case GameState.Menu:
                    break;
                case GameState.CG:

                    break;
                case GameState.Running:
                    break;

                case GameState.Paused:
                    break;

                case GameState.Exit:
                    break;
            }

            CurState = newState;
            EventBus<GameStateChanged>.Publish(new GameStateChanged(CurState));
        }

        public void Resume()
        {
            if (CurState != GameState.Paused) return;

            GameState target = _hasStateBeforePause ? _stateBeforePause : GameState.Running;
            _hasStateBeforePause = false;

            // 场景入口填 Paused 时 before 可能是 None/SceneTransition；不回落到 Running 就永远解不开 timeScale=0
            if (target == GameState.None || target == GameState.SceneTransition || target == GameState.Settle)
                target = GameState.Running;

            ChangeState(target);
        }

        public void BeginSceneTransition()
        {
            if (CurState == GameState.SceneTransition) return;

            _stateBeforeSceneTransition = CurState;
            _hasSceneTransitionTarget = false;
            _sceneTransitionFadeFinished = false;
            _sceneTransitionFailed = false;
            ChangeState(GameState.SceneTransition);
        }

        public void SetSceneTransitionTargetState(GameState targetState)
        {
            if (targetState == GameState.None
                || targetState == GameState.Exit
                || targetState == GameState.Settle
                || targetState == GameState.SceneTransition)
            {
                Debug.LogError($"[Game] 场景入口状态无效：{targetState}。");
                return;
            }

            if (CurState != GameState.SceneTransition)
            {
                ChangeState(targetState);
                return;
            }

            _sceneTransitionTargetState = targetState;
            _hasSceneTransitionTarget = true;
            TryFinishSceneTransition();
        }

        // 遮罩未淡出时供 UI 预摆入口面板
        public bool TryGetPendingSceneEntryState(out GameState state)
        {
            state = _sceneTransitionTargetState;

            return CurState == GameState.SceneTransition && _hasSceneTransitionTarget;
        }

        public void FinishSceneTransition(bool success)
        {
            if (CurState != GameState.SceneTransition) return;

            _sceneTransitionFailed = !success;
            _sceneTransitionFadeFinished = true;
            TryFinishSceneTransition();
        }

        private void TryFinishSceneTransition()
        {
            if (!_sceneTransitionFadeFinished) return;

            GameState targetState;
            if (_sceneTransitionFailed)
            {
                targetState = _stateBeforeSceneTransition;
            }
            else
            {
                if (!_hasSceneTransitionTarget) return;
                targetState = _sceneTransitionTargetState;
            }

            _hasSceneTransitionTarget = false;
            _sceneTransitionFadeFinished = false;
            _sceneTransitionFailed = false;
            _isFinalizingSceneTransition = true;
            try
            {
                ChangeState(targetState);
            }
            finally
            {
                _isFinalizingSceneTransition = false;
            }
        }
    }
}
