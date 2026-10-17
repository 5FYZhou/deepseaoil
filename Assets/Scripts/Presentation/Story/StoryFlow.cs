using System;
using System.Collections.Generic;
using DeepseaOil.Data;
using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Services.Time;
using DeepseaOil.Logic.Level;
using DeepseaOil.Logic.Wave;
using DeepseaOil.Presentation.Input;
using DeepseaOil.Presentation.World;
using DeepseaOil.Presentation.UI;
using Cinemachine;
using UnityEngine;
using cfg.dso;

namespace DeepseaOil.Presentation.Story
{
    public sealed class StoryFlow : MonoBehaviour, ISceneRoot, IRenderTicked
    {
        private const float PopupDurationSeconds = 3f;

        private enum FlowStage
        {
            None,
            Opening,
            WaitingForFacility,
            TutorialResourceDialogue,
            TutorialEnemyDialogue,
            LevelTwoDialogue,
            LevelThreeDialogue,
            LevelFourDialogue,
            WaitingForSeed,
            SeedDialogue,
            LevelFiveDialogue,
            LevelFiveWaveDialogue,
            LevelSixDialogue,
            LevelSixWaveDialogue,
            EndingDialogue,
        }

        private readonly List<DialogueSpec> _popupLines = new();
        private readonly Queue<int> _pendingStoryParagraphs = new();

        private GameRoot _root;
        private CombatRoot _combatRoot;
        private PlayerMoveTrigger _playerMoveTrigger;
        private IReadOnlyList<DialogueSpec> _lines;
        private DialoguePanel _panel;
        private CinemachineVirtualCamera _storyCamera;
        private Transform _originalCameraFollow;
        private bool _hasOriginalCameraFollow;
        private TimerHandle _popupTimer;
        private int _paragraph;
        private int _lineIndex;
        private int _popupIndex;
        private int _playbackVersion;
        private GameState _resumeGameState;
        private FlowStage _stage;
        private bool _playerLeftMoveTrigger;
        private bool _seedDialogueQueued;
        private bool _levelFiveWaveDialogueQueued;
        private bool _levelSixWaveTwoDialogueQueued;
        private bool _levelSixWaveThreeDialogueQueued;
        private bool _endingDialogueQueued;
        private bool _attached;
        private bool _playing;
        private bool _waitingForContinue;
        private bool _continueRequested;
        private bool _popupActive;
        private bool _popupExpired;
        private bool _hasResumeGameState;

        public int Order => SceneOrder.World + 1;

        private FlowStage Stage
        {
            get => _stage;
            set
            {
                _stage = value;
                EventBus<StoryFlowStateChanged>.Publish(new StoryFlowStateChanged(value.ToString()));
            }
        }

        public event Action<int> ParagraphCompleted;

        private void Start()
        {
            if (_root != null) return;

            _root = GameRoot.Instance;
            if (_root == null)
            {
                Debug.LogError("[StoryFlow] GameRoot 不存在，剧情流程无法注册。", this);
                enabled = false;
                return;
            }

            _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null)
                _root.UnregisterSceneRoot(this);

            EventBus<DropCollected>.Unsubscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Unsubscribe(OnRequestHudRefresh);
            EventBus<WavePhaseChanged>.Unsubscribe(OnWavePhaseChanged);
            EventBus<LevelStateChanged>.Unsubscribe(OnLevelStateChanged);
            if (_combatRoot != null && _playerMoveTrigger != null)
            {
                _playerMoveTrigger.PlayerExited -= OnPlayerMoveTriggerExited;
            }
            EventBus<GameStateChanged>.Unsubscribe(OnGameStateChanged);

            SetPlayerInputLocked(false);
            CancelParagraph();
        }

        public void Initialize(CombatRoot combatRoot, GameRoot root, PlayerMoveTrigger playerMoveTrigger)
        {
            if (combatRoot == null || root == null)
            {
                Debug.LogError("[StoryFlow] 初始化缺少 CombatRoot 或 GameRoot。", this);
                enabled = false;
                return;
            }

            _combatRoot = combatRoot;
            _root = root;
            _playerMoveTrigger = playerMoveTrigger;
            _root.RegisterSceneRoot(this);
        }

        public void Attach()
        {
            if (_attached) return;

            _attached = true;
            EventBus<DropCollected>.Subscribe(OnDropCollected);
            EventBus<RequestHudRefresh>.Subscribe(OnRequestHudRefresh);
            EventBus<GameStateChanged>.Subscribe(OnGameStateChanged);
            EventBus<WavePhaseChanged>.Subscribe(OnWavePhaseChanged);
            EventBus<LevelStateChanged>.Subscribe(OnLevelStateChanged);

            int levelId = _combatRoot.CurrentLevelId;
            if (levelId == 1)
            {
                if (_playerMoveTrigger == null)
                    Debug.LogError("[StoryFlow] 第一关未接入 PlayerMoveTrigger，无法检测玩家离开触发区。", this);
                else
                    _playerMoveTrigger.PlayerExited += OnPlayerMoveTriggerExited;
            }

            switch (levelId)
            {
                case 1:
                    SetPlayerInputLocked(true);
                    BeginParagraph(1001, FlowStage.Opening);
                    break;
                case 2:
                    SetPlayerInputLocked(true);
                    BeginParagraph(2001, FlowStage.LevelTwoDialogue);
                    break;
                case 3:
                    SetPlayerInputLocked(true);
                    BeginParagraph(3001, FlowStage.LevelThreeDialogue);
                    break;
                case 4:
                    SetPlayerInputLocked(true);
                    BeginParagraph(4001, FlowStage.LevelFourDialogue);
                    break;
                case 5:
                    SetPlayerInputLocked(true);
                    BeginParagraph(5001, FlowStage.LevelFiveDialogue);
                    break;
                case 6:
                    SetPlayerInputLocked(true);
                    BeginParagraph(6001, FlowStage.LevelSixDialogue);
                    break;
            }
        }

        public void RenderTick(float deltaTime)
        {
            if (_popupExpired && _popupActive)
            {
                ProcessPopupExpired();
                if (!_waitingForContinue)
                    ResumeGameAfterDialogue();
            }

            if (_playing && _continueRequested)
            {
                _continueRequested = false;
                _waitingForContinue = false;
                _panel.HideDialogueContent();
                ProcessLines();
            }

            UpdateTutorialTriggers();
        }

        private bool BeginParagraph(int paragraph, FlowStage stage)
        {
            return PlayParagraph(paragraph, stage);
        }

        public bool PlayParagraph(int paragraph)
        {
            return PlayParagraph(paragraph, FlowStage.None);
        }

        private bool PlayParagraph(int paragraph, FlowStage stage)
        {
            if (!_attached)
            {
                Debug.LogError("[StoryFlow] 必须先由 GameRoot.Attach 场景根后才能播放剧情段。", this);
                return false;
            }

            if (_playing)
            {
                Debug.LogError(
                    $"[StoryFlow] 段 {_paragraph} 尚未播放完，拒绝并行播放段 {paragraph}。",
                    this);
                return false;
            }
            if (_root.UI == null || _root.Timer == null)
            {
                Debug.LogError("[StoryFlow] UIMgr 或 TimerManager 未就绪，无法播放剧情。", this);
                return false;
            }
            if (_root.Game == null)
            {
                Debug.LogError("[StoryFlow] GameManager 未就绪，无法播放剧情。", this);
                return false;
            }
            IReadOnlyList<DialogueSpec> lines = ConfigModule.GetDialogueParagraph(paragraph);
            if (lines.Count == 0)
            {
                Debug.LogError($"[StoryFlow] 对话表中不存在剧情段 {paragraph}。", this);
                return false;
            }

            _paragraph = paragraph;
            Stage = stage;
            _lines = lines;
            _lineIndex = 0;
            _popupIndex = 0;
            _popupLines.Clear();
            _waitingForContinue = false;
            _continueRequested = false;
            _popupActive = false;
            _popupExpired = false;
            _popupTimer = TimerHandle.Invalid;
            _playing = true;

            int version = ++_playbackVersion;
            _root.UI.ShowPanel<DialoguePanel>(panel => OnPanelReady(panel, version));
            return true;
        }

        public void CancelParagraph()
        {
            if (!_playing && _panel == null)
            {
                ResumeGameAfterDialogue();
                return;
            }

            _playing = false;
            _playbackVersion++;
            _waitingForContinue = false;
            _continueRequested = false;
            _popupExpired = false;
            _popupActive = false;
            _popupLines.Clear();
            _popupIndex = 0;
            _lines = null;

            if (_popupTimer.IsValid && _root != null && _root.Timer != null)
                _root.Timer.Cancel(_popupTimer);

            _popupTimer = TimerHandle.Invalid;
            DetachPanel();

            if (_root != null && _root.UI != null)
                _root.UI.HidePanel<DialoguePanel>();

            ResumeGameAfterDialogue();
        }

        private void OnPanelReady(DialoguePanel panel, int version)
        {
            if (!_playing || version != _playbackVersion) return;

            if (panel == null)
            {
                Debug.LogError("[StoryFlow] DialoguePanel 加载完成回调收到空面板。", this);
                CancelParagraph();
                return;
            }

            _panel = panel;
            _panel.ContinueRequested += OnContinueRequested;
            ProcessLines();
        }

        private void ProcessLines()
        {
            while (_playing && !_waitingForContinue && _lineIndex < _lines.Count)
            {
                DialogueSpec line = _lines[_lineIndex++];
                OnLineStarting(line);

                switch (line.Type)
                {
                    case DialogType.Npc:
                    case DialogType.Machine:
                        PauseGameForDialogue();
                        if (!_panel.ShowDialogue(line))
                        {
                            CancelParagraph();
                            return;
                        }

                        _waitingForContinue = true;
                        PlaySfx(line.Sfx);
                        return;

                    case DialogType.NpcPopup:
                    case DialogType.PlayerPopup:
                        SetPlayerInputLocked(false);
                        _popupLines.Add(line);
                        if (!_popupActive)
                            ShowNextPopup();
                        break;

                    default:
                        Debug.LogError(
                            $"[StoryFlow] 对话段 {_paragraph} 的行 {line.Id} 有无效类型 {line.Type}。",
                            this);
                        CancelParagraph();
                        return;
                }
            }

            TryCompleteParagraph();
            if (!_waitingForContinue)
                ResumeGameAfterDialogue();
        }

        private void OnLineStarting(DialogueSpec line)
        {
            if (_paragraph == 1002)
            {
                switch (line.Id)
                {
                    case 7:
                        FocusCamera(FountainKind.Water);
                        break;
                    case 8:
                        FocusCamera(FountainKind.Earth);
                        break;
                    case 9:
                        FocusCamera(FountainKind.Life);
                        break;
                    case 10:
                        RestoreCameraFollow();
                        break;
                }
            }

            if (IsLevelIntroParagraph(_paragraph)
                && (line.Type == DialogType.PlayerPopup || line.Type == DialogType.NpcPopup))
            {
                _combatRoot.BeginLevel();
                SetPlayerInputLocked(false);
            }
        }

        private void OnContinueRequested()
        {
            if (_playing && _waitingForContinue)
                _continueRequested = true;
        }

        private void ShowNextPopup()
        {
            if (_popupIndex >= _popupLines.Count) return;

            DialogueSpec line = _popupLines[_popupIndex];
            if (!_panel.ShowPopup(line))
            {
                CancelParagraph();
                return;
            }

            _popupActive = true;
            _popupTimer = _root.Timer.ScheduleUnscaled(PopupDurationSeconds, OnPopupTimerElapsed);
            if (!_popupTimer.IsValid)
            {
                Debug.LogError("[StoryFlow] 未能安排弹窗的现实时间计时器。", this);
                CancelParagraph();
                return;
            }

            PlaySfx(line.Sfx);
        }

        private void OnPopupTimerElapsed()
        {
            _popupExpired = true;
        }

        private void ProcessPopupExpired()
        {
            _popupExpired = false;
            _popupActive = false;
            _popupTimer = TimerHandle.Invalid;
            _panel.HidePopupContent();
            _popupIndex++;

            if (_popupIndex < _popupLines.Count)
            {
                ShowNextPopup();
                return;
            }

            _popupLines.Clear();
            _popupIndex = 0;
            TryCompleteParagraph();
        }

        private void TryCompleteParagraph()
        {
            if (!_playing
                || _lineIndex < _lines.Count
                || _waitingForContinue
                || _popupActive
                || _popupLines.Count > 0)
            {
                return;
            }
            int completedParagraph = _paragraph;
            _playing = false;
            _lines = null;
            DetachPanel();
            _root.UI.HidePanel<DialoguePanel>();
            OnParagraphCompleted(completedParagraph);
            ParagraphCompleted?.Invoke(completedParagraph);
        }

        private void OnParagraphCompleted(int paragraph)
        {
            switch (Stage)
            {
                case FlowStage.Opening:
                    Stage = FlowStage.WaitingForFacility;
                    break;
                case FlowStage.TutorialResourceDialogue:
                    RestoreCameraFollow();
                    _combatRoot.BeginLevel();
                    BeginParagraph(1003, FlowStage.TutorialEnemyDialogue);
                    break;
                case FlowStage.TutorialEnemyDialogue:
                    SetPlayerInputLocked(false);
                    _combatRoot.ReleaseDeferredLevelEnd();
                    break;
                case FlowStage.LevelTwoDialogue:
                case FlowStage.LevelThreeDialogue:
                case FlowStage.LevelFourDialogue:
                case FlowStage.LevelFiveDialogue:
                case FlowStage.LevelSixDialogue:
                    SetPlayerInputLocked(false);
                    if (Stage == FlowStage.LevelFourDialogue)
                        Stage = FlowStage.WaitingForSeed;
                    break;
                case FlowStage.SeedDialogue:
                    Stage = FlowStage.WaitingForSeed;
                    SetPlayerInputLocked(false);
                    break;
                case FlowStage.LevelFiveWaveDialogue:
                case FlowStage.LevelSixWaveDialogue:
                    SetPlayerInputLocked(false);
                    break;
                case FlowStage.EndingDialogue:
                    SetPlayerInputLocked(false);
                    _root.Game.ChangeState(GameState.Settle);
                    _combatRoot.ReleaseDeferredLevelEnd();
                    break;
            }

            TryBeginPendingStoryParagraph();
        }

        private void UpdateTutorialTriggers()
        {
            if (!_attached || _combatRoot == null) return;

            if (Stage == FlowStage.WaitingForFacility && _playerLeftMoveTrigger)
            {
                _playerLeftMoveTrigger = false;
                SetPlayerInputLocked(true);
                BeginParagraph(1002, FlowStage.TutorialResourceDialogue);
                return;
            }

        }

        private void OnPlayerMoveTriggerExited(PlayerController player)
        {
            if (player != _combatRoot.Player)
                return;

            if (Stage == FlowStage.Opening || Stage == FlowStage.WaitingForFacility)
                _playerLeftMoveTrigger = true;
        }

        private void OnDropCollected(DropCollected evt)
        {
            if (_combatRoot.CurrentLevelId != 4
                || evt.Type != DropType.Seed
                || evt.Seed == SeedType.None
                || _seedDialogueQueued)
            {
                return;
            }

            _seedDialogueQueued = true;
            EnqueueStoryParagraph(4002);
        }

        private void OnWavePhaseChanged(WavePhaseChanged evt)
        {
            if (evt.Phase != WavePhase.Prep) return;

            int levelId = _combatRoot.CurrentLevelId;
            if (levelId == 5 && evt.WaveIndex == 2 && !_levelFiveWaveDialogueQueued)
            {
                _levelFiveWaveDialogueQueued = true;
                EnqueueStoryParagraph(5002);
            }
            else if (levelId == 6 && evt.WaveIndex == 2 && !_levelSixWaveTwoDialogueQueued)
            {
                _levelSixWaveTwoDialogueQueued = true;
                EnqueueStoryParagraph(6002);
            }
            else if (levelId == 6 && evt.WaveIndex == 3 && !_levelSixWaveThreeDialogueQueued)
            {
                _levelSixWaveThreeDialogueQueued = true;
                EnqueueStoryParagraph(6003);
            }
        }

        private void OnLevelStateChanged(LevelStateChanged evt)
        {
            if (_combatRoot.CurrentLevelId != 6
                || evt.Level != LevelState.Victory
                || _endingDialogueQueued)
            {
                return;
            }

            _endingDialogueQueued = true;
            EnqueueStoryParagraph(7001);
        }

        private void EnqueueStoryParagraph(int paragraph)
        {
            _pendingStoryParagraphs.Enqueue(paragraph);
            TryBeginPendingStoryParagraph();
        }

        private void TryBeginPendingStoryParagraph()
        {
            if (!_attached || _playing || _pendingStoryParagraphs.Count == 0)
                return;

            int paragraph = _pendingStoryParagraphs.Dequeue();
            FlowStage stage;
            switch (paragraph)
            {
                case 4002:
                    stage = FlowStage.SeedDialogue;
                    break;
                case 5002:
                    stage = FlowStage.LevelFiveWaveDialogue;
                    break;
                case 6002:
                case 6003:
                    stage = FlowStage.LevelSixWaveDialogue;
                    break;
                case 7001:
                    stage = FlowStage.EndingDialogue;
                    break;
                default:
                    Debug.LogError($"[StoryFlow] 未处理的排队剧情段 {paragraph}。", this);
                    return;
            }

            SetPlayerInputLocked(true);
            BeginParagraph(paragraph, stage);
        }

        private static bool IsLevelIntroParagraph(int paragraph)
        {
            return paragraph == 2001
                || paragraph == 3001
                || paragraph == 4001
                || paragraph == 5001
                || paragraph == 6001;
        }

        private void SetPlayerInputLocked(bool locked)
        {
            InputProvider input = _combatRoot != null && _combatRoot.Player != null
                ? _combatRoot.Player.Input
                : null;
            input?.SetStoryInputLocked(locked);
        }

        private void PauseGameForDialogue()
        {
            if (!_hasResumeGameState)
            {
                _resumeGameState = _root.Game.CurState == GameState.SceneTransition
                    ? GameState.Running
                    : _root.Game.CurState;
                _hasResumeGameState = true;
            }

            if (_root.Game.CurState != GameState.CG && _root.Game.CurState != GameState.SceneTransition)
                _root.Game.ChangeState(GameState.CG);
        }

        private void OnRequestHudRefresh(RequestHudRefresh evt)
        {
            EventBus<StoryFlowStateChanged>.Publish(new StoryFlowStateChanged(Stage.ToString()));
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            if (!_playing || !_hasResumeGameState || evt.state != _resumeGameState)
                return;

            if (_root != null && _root.Game != null && _root.Game.CurState != GameState.CG)
                _root.Game.ChangeState(GameState.CG);
        }

        private void ResumeGameAfterDialogue()
        {
            if (!_hasResumeGameState) return;

            GameState resumeState = _resumeGameState;
            _hasResumeGameState = false;
            _resumeGameState = GameState.None;

            if (_root != null && _root.Game != null && _root.Game.CurState == GameState.CG)
                _root.Game.ChangeState(resumeState);
        }

        private void FocusCamera(FountainKind kind)
        {
            Fountain target = null;
            IReadOnlyList<Fountain> fountains = _combatRoot.Fountains;
            for (int i = 0; i < fountains.Count; i++)
            {
                if (fountains[i] != null && fountains[i].Kind == kind)
                {
                    target = fountains[i];
                    break;
                }
            }

            if (target == null)
            {
                Debug.LogError($"[StoryFlow] 缺少 {kind} 教学镜头目标。", this);
                return;
            }

            if (_storyCamera == null)
                FindStoryCamera();

            if (_storyCamera == null)
            {
                Debug.LogError("[StoryFlow] 场景中找不到 CinemachineVirtualCamera，不能聚焦教学目标。", this);
                return;
            }

            if (!_hasOriginalCameraFollow)
            {
                _originalCameraFollow = _storyCamera.Follow;
                _hasOriginalCameraFollow = true;
            }

            _storyCamera.Follow = target.transform;
            _storyCamera.LookAt = target.transform;
            Vector3 cameraPosition = _storyCamera.transform.position;
            Vector3 targetPosition = target.transform.position;
            cameraPosition.x = targetPosition.x;
            cameraPosition.y = targetPosition.y;
            _storyCamera.ForceCameraPosition(cameraPosition, _storyCamera.transform.rotation);
        }

        private void FindStoryCamera()
        {
            CinemachineVirtualCamera[] cameras = FindObjectsOfType<CinemachineVirtualCamera>();
            if (cameras.Length == 0) return;

            _storyCamera = cameras[0];
            for (int i = 1; i < cameras.Length; i++)
            {
                if (cameras[i].Priority > _storyCamera.Priority)
                    _storyCamera = cameras[i];
            }
        }

        private void RestoreCameraFollow()
        {
            if (_storyCamera == null) return;

            Transform follow = _combatRoot.Player != null
                ? _combatRoot.Player.transform
                : _hasOriginalCameraFollow ? _originalCameraFollow : null;

            if (follow != null)
            {
                _storyCamera.Follow = follow;
                _storyCamera.LookAt = follow;
                Vector3 cameraPosition = _storyCamera.transform.position;
                Vector3 playerPosition = follow.position;
                cameraPosition.x = playerPosition.x;
                cameraPosition.y = playerPosition.y;
                _storyCamera.ForceCameraPosition(cameraPosition, _storyCamera.transform.rotation);
            }

            _originalCameraFollow = null;
            _hasOriginalCameraFollow = false;
        }

        private void PlaySfx(AudioId id)
        {
            if (id == AudioId.None) return;

            if (_root == null || _root.Audio == null)
            {
                Debug.LogError($"[StoryFlow] GameRoot.Audio 不可用，无法播放剧情音效 {id}。", this);
                return;
            }

            if(id == AudioId.BgmMain || id == AudioId.BgmAscensio)
            {
                _root.Audio.EnqueueBgm(id);
                return;
            }

            _root.Audio.EnqueueSfx(id);
        }

        private void DetachPanel()
        {
            if (_panel == null) return;

            _panel.ContinueRequested -= OnContinueRequested;
            _panel = null;
        }
    }
}
