using System;
using System.Globalization;
using System.IO;
using System.Security;
using DeepseaOil.Data;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Logic.Service
{
    public enum SaveSlotStatus
    {
        Empty,
        Available,
        Invalid
    }

    public readonly struct SaveSlotInfo
    {
        public const int SlotCount = 4;

        public int SlotIndex { get; }

        public SaveSlotStatus Status { get; }

        public string SavedAtUtc { get; }

        public int CurrentLevelId { get; }

        public string Title { get; }

        public string TimeText { get; }

        public string Error { get; }

        public float PlayerHp { get; }

        public float PlayerMaxHp { get; }

        public SaveSlotInfo(
            int slotIndex,
            SaveSlotStatus status,
            string savedAtUtc,
            int currentLevelId,
            string title,
            string timeText,
            string error,
            float playerHp = 0f,
            float playerMaxHp = 0f)
        {
            SlotIndex = slotIndex;
            Status = status;
            SavedAtUtc = savedAtUtc;
            CurrentLevelId = currentLevelId;
            Title = title;
            TimeText = timeText;
            Error = error;
            PlayerHp = playerHp;
            PlayerMaxHp = playerMaxHp;
        }
    }

    public sealed class SaveService : IService
    {
        public const int SlotCount = SaveSlotInfo.SlotCount;

        private const string SaveDirectoryName = "saves";
        private const string SaveFilePrefix = "slot_";
        private const string SaveFileExtension = ".json";
        private const string ThumbnailFileSuffix = "_thumb.png";
        private const string TimeFormat = "O";
        private const string TimeTextFormat = "yyyy/MM/dd HH:mm";

        private GameSaveData _pendingRestore;

        public SaveService() { }

        public void Init()
        {
            EventBus<RequestSave>.Subscribe(OnRequestSave);
            EventBus<RequestAutoSave>.Subscribe(OnRequestAutoSave);
            EventBus<RequestLoad>.Subscribe(OnRequestLoad);
            EventBus<RequestDeleteSave>.Subscribe(OnRequestDeleteSave);
            EventBus<RequestHudRefresh>.Subscribe(OnRequestHudRefresh);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
        }

        public SaveSlotInfo GetSlotHeader(int slotIndex)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                return new SaveSlotInfo(
                    slotIndex,
                    SaveSlotStatus.Invalid,
                    null,
                    0,
                    null,
                    null,
                    $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。");
            }

            if (!TryReadSaveFile(
                slotIndex,
                out GameSaveData data,
                out bool exists,
                out string error))
            {
                return new SaveSlotInfo(slotIndex, SaveSlotStatus.Invalid, null, 0, null, null, error);
            }

            if (!exists)
            {
                return new SaveSlotInfo(slotIndex, SaveSlotStatus.Empty, null, 0, null, null, null);
            }

            if (!string.IsNullOrEmpty(error))
            {
                return new SaveSlotInfo(slotIndex, SaveSlotStatus.Invalid, null, 0, null, null, error);
            }

            return new SaveSlotInfo(
                slotIndex,
                SaveSlotStatus.Available,
                data.header.savedAtUtc,
                data.header.currentLevelId,
                data.header.title,
                data.header.timeText,
                null,
                data.player != null ? data.player.hp : 0f,
                data.player != null ? data.player.maxHp : 0f);
        }

        public SaveSlotInfo[] GetSlotInfos()
        {
            SaveSlotInfo[] slots = new SaveSlotInfo[SlotCount];

            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                slots[slotIndex] = GetSlotHeader(slotIndex);
            }

            return slots;
        }

        public bool TrySaveState(int slotIndex, GameSaveData data, out string error)
        {
            error = null;

            if (!IsValidSlotIndex(slotIndex))
            {
                error = $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。";
                return false;
            }

            if (data == null || data.header == null)
            {
                error = "存档内容为空，无法保存进度。";
                return false;
            }

            if (data.header.currentLevelId <= 0)
            {
                error = "当前关卡编号无效，无法保存进度。";
                return false;
            }

            bool isAuto = slotIndex == 0;

            data.schemaVersion = GameSaveData.CurrentSchemaVersion;
            data.header.schemaVersion = GameSaveData.CurrentSchemaVersion;
            data.header.slotIndex = slotIndex;
            data.header.isAuto = isAuto;
            data.header.savedAtUtc = DateTimeOffset.UtcNow.ToString(TimeFormat, CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(data.header.title))
            {
                data.header.title = isAuto ? "自动存档" : $"存档槽 {slotIndex + 1}";
            }

            data.header.timeText = FormatLocalTime(data.header.savedAtUtc);

            string json;

            try
            {
                json = JsonUtility.ToJson(data);
            }
            catch (ArgumentException ex)
            {
                error = $"存档内容无法序列化：{ex.Message}";
                return false;
            }

            return TryReplaceFile(GetSaveFilePath(slotIndex), json, slotIndex, true, out error);
        }

        public bool TryLoadState(int slotIndex, out GameSaveData data, out string error)
        {
            data = null;
            error = null;

            if (!IsValidSlotIndex(slotIndex))
            {
                error = $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。";
                return false;
            }

            if (!TryReadSaveFile(slotIndex, out data, out bool exists, out error))
            {
                data = null;
                return false;
            }

            if (!exists)
            {
                data = null;
                error = $"存档槽 {slotIndex} 为空。";
                return false;
            }

            if (!string.IsNullOrEmpty(error))
            {
                data = null;
                return false;
            }

            return true;
        }

        public bool TryDelete(int slotIndex, out string error)
        {
            error = null;

            if (!IsValidSlotIndex(slotIndex))
            {
                error = $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。";
                return false;
            }

            try
            {
                File.Delete(GetSaveFilePath(slotIndex));
                File.Delete(GetThumbnailFilePath(slotIndex));

                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                error = $"删除存档槽 {slotIndex} 失败：{ex.Message}";
                return false;
            }
        }

        public void AttachThumbnail(int slotIndex, byte[] png)
        {
            if (!IsValidSlotIndex(slotIndex) || png == null || png.Length == 0) return;

            TryReplaceFile(GetThumbnailFilePath(slotIndex), png, slotIndex, false, out string error);

            if (error != null)
            {
                Debug.LogWarning($"[Save] 存档缩略图写入失败：{error}");

                return;
            }

            PublishSlotInfos();
        }

        public bool TryGetThumbnail(int slotIndex, out byte[] png)
        {
            png = null;

            if (!IsValidSlotIndex(slotIndex)) return false;

            string filePath = GetThumbnailFilePath(slotIndex);

            try
            {
                if (!File.Exists(filePath)) return false;

                png = File.ReadAllBytes(filePath);
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                Debug.LogWarning($"[Save] 存档缩略图读取失败：{ex.Message}");

                png = null;

                return false;
            }

            if (png != null && png.Length > 0) return true;

            png = null;

            return false;
        }

        public void SetPendingRestore(GameSaveData data)
        {
            _pendingRestore = data;
        }

        public bool TryConsumePendingRestore(out GameSaveData data)
        {
            data = _pendingRestore;
            _pendingRestore = null;

            return data != null;
        }

        public void Dispose()
        {
            _pendingRestore = null;

            EventBus<RequestSave>.Unsubscribe(OnRequestSave);
            EventBus<RequestAutoSave>.Unsubscribe(OnRequestAutoSave);
            EventBus<RequestLoad>.Unsubscribe(OnRequestLoad);
            EventBus<RequestDeleteSave>.Unsubscribe(OnRequestDeleteSave);
            EventBus<RequestHudRefresh>.Unsubscribe(OnRequestHudRefresh);
        }

        private void OnRequestHudRefresh(RequestHudRefresh evt)
        {
            PublishSlotInfos();
        }

        private void OnRequestSave(RequestSave evt)
        {
            if (evt.SlotIndex == 0)
            {
                EventBus<SaveResult>.Publish(
                    new SaveResult(evt.SlotIndex, false, "0 号槽为自动存档槽，不能手动保存。"));

                return;
            }

            if (TrySaveState(evt.SlotIndex, evt.Data, out string error))
            {
                PublishSlotInfos();
                EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, true, null));

                return;
            }

            EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, false, error));
        }

        private void OnRequestAutoSave(RequestAutoSave evt)
        {
            if (TrySaveState(0, evt.Data, out string error))
            {
                PublishSlotInfos();
                EventBus<SaveResult>.Publish(new SaveResult(0, true, null));

                return;
            }

            Debug.LogError($"[Save] 0 号自动存档失败：{error}");

            EventBus<SaveResult>.Publish(new SaveResult(0, false, error));
        }

        private void OnRequestDeleteSave(RequestDeleteSave evt)
        {
            if (TryDelete(evt.SlotIndex, out string error))
            {
                PublishSlotInfos();
                EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, true, null));

                return;
            }

            EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, false, error));
        }

        private void OnRequestLoad(RequestLoad evt)
        {
            if (!TryLoadState(evt.SlotIndex, out GameSaveData data, out string error))
            {
                EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, false, error));

                return;
            }

            _pendingRestore = data;

            EventBus<SaveLoaded>.Publish(new SaveLoaded(evt.SlotIndex, data));
            EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, true, null));
        }

        private void PublishSlotInfos()
        {
            EventBus<SaveSlotsRefreshed>.Publish(new SaveSlotsRefreshed(GetSlotInfos()));
        }

        private static bool TryReadSaveFile(
            int slotIndex,
            out GameSaveData data,
            out bool exists,
            out string error)
        {
            data = null;
            error = null;
            exists = true;

            string filePath = GetSaveFilePath(slotIndex);
            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (FileNotFoundException)
            {
                exists = false;
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                exists = false;
                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                error = $"读取存档槽 {slotIndex} 失败：{ex.Message}";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (ArgumentException ex)
            {
                error = $"存档槽 {slotIndex} 的 JSON 无法解析：{ex.Message}";
                return true;
            }

            if (data == null || data.header == null)
            {
                error = $"存档槽 {slotIndex} 的 JSON 内容为空或无效。";
                return true;
            }

            if (data.schemaVersion != GameSaveData.CurrentSchemaVersion)
            {
                error = $"存档槽 {slotIndex} 的格式版本 {data.schemaVersion} 不受支持。";
                return true;
            }

            if (data.header.slotIndex != slotIndex)
            {
                error = $"存档文件槽位编号不匹配：预期 {slotIndex}，实际 {data.header.slotIndex}。";
                return true;
            }

            if (data.header.currentLevelId <= 0)
            {
                error = $"存档槽 {slotIndex} 的关卡编号无效。";
                return true;
            }

            if (!DateTimeOffset.TryParseExact(
                    data.header.savedAtUtc,
                    TimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTimeOffset savedAtUtc)
                || savedAtUtc.Offset != TimeSpan.Zero)
            {
                error = $"存档槽 {slotIndex} 的保存时间无效。";
                return true;
            }

            return true;
        }

        private static bool TryReplaceFile(
            string filePath,
            string text,
            int slotIndex,
            bool isSave,
            out string error)
        {
            error = null;

            string temporaryPath = filePath + ".tmp";

            try
            {
                Directory.CreateDirectory(GetSaveDirectoryPath());
                File.WriteAllText(temporaryPath, text);

                if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
                else File.Move(temporaryPath, filePath);

                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                error = isSave
                    ? $"写入存档槽 {slotIndex} 失败：{ex.Message}"
                    : $"写入存档槽 {slotIndex} 的缩略图失败：{ex.Message}";

                TryDeleteTemporaryFile(temporaryPath);

                return false;
            }
        }

        private static bool TryReplaceFile(
            string filePath,
            byte[] bytes,
            int slotIndex,
            bool isSave,
            out string error)
        {
            error = null;

            string temporaryPath = filePath + ".tmp";

            try
            {
                Directory.CreateDirectory(GetSaveDirectoryPath());
                File.WriteAllBytes(temporaryPath, bytes);

                if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
                else File.Move(temporaryPath, filePath);

                return true;
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                error = isSave
                    ? $"写入存档槽 {slotIndex} 失败：{ex.Message}"
                    : $"写入存档槽 {slotIndex} 的缩略图失败：{ex.Message}";

                TryDeleteTemporaryFile(temporaryPath);

                return false;
            }
        }

        private static string FormatLocalTime(string savedAtUtc)
        {
            if (DateTimeOffset.TryParseExact(
                savedAtUtc,
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset parsed))
            {
                return parsed.ToLocalTime().ToString(TimeTextFormat, CultureInfo.InvariantCulture);
            }

            return "--";
        }

        private static bool IsIoFailure(Exception ex)
        {
            return ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is SecurityException;
        }

        private static string GetSaveDirectoryPath()
        {
            return Path.Combine(Application.persistentDataPath, SaveDirectoryName);
        }

        private static string GetSaveFilePath(int slotIndex)
        {
            return Path.Combine(
                GetSaveDirectoryPath(),
                SaveFilePrefix + slotIndex.ToString(CultureInfo.InvariantCulture) + SaveFileExtension);
        }

        private static string GetThumbnailFilePath(int slotIndex)
        {
            return Path.Combine(
                GetSaveDirectoryPath(),
                SaveFilePrefix + slotIndex.ToString(CultureInfo.InvariantCulture) + ThumbnailFileSuffix);
        }

        private static bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                Debug.LogWarning($"无法清理临时存档文件 '{path}'：{ex.Message}");
            }
        }
    }
}
