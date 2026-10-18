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

        private const float CameraGlideSeconds = 0.7f;

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
        private Transform _cameraTransform;
        private Vector3 _cameraOffset;
        private Vector3 _cameraGlideFrom;
        private Vector3 _cameraGlideTo;
        private float _cameraGlideElapsed;
        private bool _cameraGliding;
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
        private StorySaveData _restoreData;
        private bool _paused;
        private bool _pendingLineStep;

        public int Order => SceneOrder.World + 1;

        /// <summary>挂上场景根之前导出的剧情进度是空的，自动存档据此等一帧</summary>
        public bool IsAttached => _attached;

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
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);

            ResetCameraFocus();
            SetPlayerInputLocked(false);
            CancelParagraph();
        }

        public void Initialize(
            CombatRoot combatRoot,
            GameRoot root,
            PlayerMoveTrigger playerMoveTrigger,
            StorySaveData restoreData = null)
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
            _restoreData = restoreData;
            _root.RegisterSceneRoot(this);
        }

        public void Attach()
        {
            if (_attached) return;

            _attached = true;
            _paused = _root.Game != null && _root.Game.CurState == GameState.Paused;
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
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

            if (_restoreData != null)
            {
                StorySaveData restore = _restoreData;
                _restoreData = null;
                RestoreProgress(restore);
                return;
            }

            BeginLevelIntro();
        }

        private void BeginLevelIntro()
        {
            switch (_combatRoot.CurrentLevelId)
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

        // 读档：已播完的段一律不再重播，只从存档停住的那一行续下去
        private void RestoreProgress(StorySaveData data)
        {
            if (!Enum.IsDefined(typeof(FlowStage), data.stage))
            {
                Debug.LogError($"[StoryFlow] 存档里的剧情阶段 {data.stage} 无效，本关剧情改为从头播放。", this);
                BeginLevelIntro();
                return;
            }

            // 进关存档点取在剧情开演之前，按从头播处理（输入锁与开场段的启动都要走原路）
            if (data.stage == (int)FlowStage.None && data.paragraph <= 0)
            {
                BeginLevelIntro();
                return;
            }

            _seedDialogueQueued = data.seedDialogueQueued;
            _levelFiveWaveDialogueQueued = data.levelFiveWaveDialogueQueued;
            _levelSixWaveTwoDialogueQueued = data.levelSixWaveTwoDialogueQueued;
            _levelSixWaveThreeDialogueQueued = data.levelSixWaveThreeDialogueQueued;
            _endingDialogueQueued = data.endingDialogueQueued;

            if (data.pendingParagraphs != null)
            {
                for (int i = 0; i < data.pendingParagraphs.Count; i++)
                    _pendingStoryParagraphs.Enqueue(data.pendingParagraphs[i]);
            }

            Stage = (FlowStage)data.stage;

            // 从对话框行续播要保持输入锁，从弹窗续播才解锁（弹窗不暂停，玩家得能动）
            IReadOnlyList<DialogueSpec> resumeLines = data.paragraph > 0
                ? ConfigModule.GetDialogueParagraph(data.paragraph)
                : null;

            SetPlayerInputLocked(StartsWithDialogueLine(resumeLines, data.lineIndex));

            if (data.paragraph > 0 && BeginParagraph(data.paragraph, Stage, data.lineIndex)) return;

            SetPlayerInputLocked(false);
            TryBeginPendingStoryParagraph();
        }

        private static bool StartsWithDialogueLine(IReadOnlyList<DialogueSpec> lines, int startLine)
        {
            return lines != null && startLine >= 0 && startLine < lines.Count && !IsPopupLine(lines[startLine]);
        }

        /// <summary>导出剧情进度；停在第 k 个弹窗上时按那一行续播</summary>
        public StorySaveData ExportSave()
        {
            var data = new StorySaveData
            {
                stage = (int)Stage,
                seedDialogueQueued = _seedDialogueQueued,
                levelFiveWaveDialogueQueued = _levelFiveWaveDialogueQueued,
                levelSixWaveTwoDialogueQueued = _levelSixWaveTwoDialogueQueued,
                levelSixWaveThreeDialogueQueued = _levelSixWaveThreeDialogueQueued,
                endingDialogueQueued = _endingDialogueQueued,
                pendingParagraphs = new List<int>(_pendingStoryParagraphs)
            };

            if (!_playing || _lines == null) return data;

            data.paragraph = _paragraph;
            data.lineIndex = ResolveResumeLine();

            return data;
        }

        private int ResolveResumeLine()
        {
            if (_popupLines.Count <= 0 || _popupIndex >= _popupLines.Count) return _lineIndex;

            int popups = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (!IsPopupLine(_lines[i])) continue;

                if (popups == _popupIndex) return i;

                popups++;
            }

            Debug.LogError($"[StoryFlow] 段 {_paragraph} 中找不到第 {_popupIndex + 1} 个弹窗行，读档将从段首重播。", this);

            return 0;
        }

        private static bool IsPopupLine(DialogueSpec line)
        {
            return line.Type == DialogType.NpcPopup || line.Type == DialogType.PlayerPopup;
        }

        public void RenderTick(float deltaTime)
        {
            // 暂停时整段剧情停车：弹窗计时走缩放时间，恢复后从剩下一秒接着走
            if (_paused) return;

            if (_pendingLineStep)
            {
                _pendingLineStep = false;
                ProcessLines();
            }

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

            // 弹窗淡出走完才算段落完成，故每帧收尾
            if (_playing)
                TryCompleteParagraph();

            UpdateTutorialTriggers();
        }

        private bool BeginParagraph(int paragraph, FlowStage stage, int startLine = 0)
        {
            return PlayParagraph(paragraph, stage, startLine);
        }

        public bool PlayParagraph(int paragraph)
        {
            return PlayParagraph(paragraph, FlowStage.None, 0);
        }

        private bool PlayParagraph(int paragraph, FlowStage stage, int startLine)
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
            _lineIndex = startLine < 0 ? 0 : (startLine > lines.Count ? lines.Count : startLine);
            _popupIndex = 0;
            _popupLines.Clear();
            _waitingForContinue = false;
            _continueRequested = false;
            _popupActive = false;
            _popupExpired = false;
            _popupTimer = TimerHandle.Invalid;
            _pendingLineStep = false;
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
            _pendingLineStep = false;
            _lines = null;
            ResetCameraFocus();

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
            _panel.SetPaused(_paused);

            // 面板可能在暂停期间加载完：真正开演推迟到 RenderTick，恢复后才推进
            _pendingLineStep = true;
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
                    case 10:
                        FocusCamera(FountainKind.Water);
                        break;
                    case 11:
                        FocusCamera(FountainKind.Earth);
                        break;
                    case 12:
                        FocusCamera(FountainKind.Life);
                        break;
                    case 13:
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

            // 走缩放时间的轮：暂停即停，恢复后接着走完这 3 秒
            _popupTimer = _root.Timer.Schedule(PopupDurationSeconds, OnPopupTimerElapsed);
            if (!_popupTimer.IsValid)
            {
                Debug.LogError("[StoryFlow] 未能安排弹窗计时器。", this);
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
                || _popupLines.Count > 0
                || (_panel != null && _panel.IsPopupVisible))
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
            _paused = evt.state == GameState.Paused;

            if (_panel != null) _panel.SetPaused(_paused);

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

            Transform cameraTransform = ResolveCameraTransform();
            if (cameraTransform == null)
            {
                Debug.LogError("[StoryFlow] 场景中找不到带 CinemachineBrain 的主相机，不能聚焦教学目标。", this);
                return;
            }

            Vector3 basePosition = cameraTransform.position - _cameraOffset;
            Vector3 offset = target.transform.position - basePosition;
            offset.z = 0f;

            StartCameraGlide(offset);
        }

        private Transform ResolveCameraTransform()
        {
            if (_cameraTransform != null) return _cameraTransform;

            Camera main = Camera.main;
            if (main == null) return null;

            _cameraTransform = main.transform;

            return _cameraTransform;
        }

        private void RestoreCameraFollow()
        {
            StartCameraGlide(Vector3.zero);
        }

        private void StartCameraGlide(Vector3 offset)
        {
            _cameraGlideFrom = _cameraOffset;
            _cameraGlideTo = offset;
            _cameraGlideElapsed = 0f;
            _cameraGliding = true;
        }

        private void ResetCameraFocus()
        {
            _cameraOffset = Vector3.zero;
            _cameraGliding = false;
        }

        // 对话期间 timeScale=0，缩放 deltaTime 恒为 0，滑行必须吃 unscaled
        private void OnCameraUpdated(CinemachineBrain brain)
        {
            Camera camera = brain != null ? brain.OutputCamera : null;
            if (camera == null) return;

            _cameraTransform = camera.transform;

            if (_cameraGliding)
            {
                if (!_paused) _cameraGlideElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_cameraGlideElapsed / CameraGlideSeconds);
                _cameraOffset = Vector3.Lerp(_cameraGlideFrom, _cameraGlideTo, t * t * (3f - 2f * t));

                if (t >= 1f)
                    _cameraGliding = false;
            }

            // Cinemachine 每帧把机位重写回基准位置，故这里直接叠加本帧偏移、不去扣上一帧的
            if (_cameraOffset.sqrMagnitude > 0f)
                camera.transform.position += _cameraOffset;
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
