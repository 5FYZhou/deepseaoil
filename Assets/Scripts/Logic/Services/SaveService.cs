using System;
using System.Globalization;
using System.IO;
using System.Security;
using DeepseaOil.Logic.Events;
using UnityEngine;

namespace DeepseaOil.Logic.Service
{
    [Serializable]
    public struct GameData
    {
        public int currentLevelId;

        public GameData(int levelId)
        {
            currentLevelId = levelId;
        }
    }

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
        public string Error { get; }

        public SaveSlotInfo(
            int slotIndex,
            SaveSlotStatus status,
            string savedAtUtc,
            int currentLevelId,
            string error)
        {
            SlotIndex = slotIndex;
            Status = status;
            SavedAtUtc = savedAtUtc;
            CurrentLevelId = currentLevelId;
            Error = error;
        }
    }

    public sealed class SaveService : IService
    {
        public const int SlotCount = SaveSlotInfo.SlotCount;
        public const int CurrentSchemaVersion = 1;

        private const string SaveDirectoryName = "saves";
        private const string SaveFilePrefix = "slot_";
        private const string SaveFileExtension = ".json";

        [Serializable]
        private sealed class SaveFileData
        {
            public int schemaVersion;
            public int slotIndex;
            public string savedAtUtc;
            public int currentLevelId;
        }

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

        public SaveSlotInfo[] GetSlotInfos()
        {
            SaveSlotInfo[] slots = new SaveSlotInfo[SlotCount];
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                if (!TryReadSaveFile(slotIndex, out SaveFileData saveFile, out bool exists, out string error))
                {
                    slots[slotIndex] = new SaveSlotInfo(
                        slotIndex,
                        SaveSlotStatus.Invalid,
                        null,
                        0,
                        error);
                    continue;
                }

                if (!exists)
                {
                    slots[slotIndex] = new SaveSlotInfo(
                        slotIndex,
                        SaveSlotStatus.Empty,
                        null,
                        0,
                        null);
                    continue;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    slots[slotIndex] = new SaveSlotInfo(
                        slotIndex,
                        SaveSlotStatus.Invalid,
                        saveFile != null ? saveFile.savedAtUtc : null,
                        saveFile != null ? saveFile.currentLevelId : 0,
                        error);
                    continue;
                }

                slots[slotIndex] = new SaveSlotInfo(
                    slotIndex,
                    SaveSlotStatus.Available,
                    saveFile.savedAtUtc,
                    saveFile.currentLevelId,
                    null);
            }

            return slots;
        }

        public bool TrySave(int slotIndex, GameData data, out string error)
        {
            error = null;
            if (!IsValidSlotIndex(slotIndex))
            {
                error = $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。";
                return false;
            }

            if (data.currentLevelId <= 0)
            {
                error = "当前关卡编号无效，无法保存进度。";
                return false;
            }

            SaveFileData saveFile = new SaveFileData
            {
                schemaVersion = CurrentSchemaVersion,
                slotIndex = slotIndex,
                savedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                currentLevelId = data.currentLevelId
            };

            string directoryPath = GetSaveDirectoryPath();
            string filePath = GetSaveFilePath(slotIndex);
            string temporaryPath = filePath + ".tmp";

            try
            {
                Directory.CreateDirectory(directoryPath);
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(saveFile));

                if (File.Exists(filePath))
                    File.Replace(temporaryPath, filePath, null);
                else
                    File.Move(temporaryPath, filePath);

                return true;
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is SecurityException)
            {
                error = $"写入存档槽 {slotIndex} 失败：{ex.Message}";
                TryDeleteTemporaryFile(temporaryPath);
                return false;
            }
        }

        public bool TryLoad(int slotIndex, out GameData data, out string error)
        {
            data = default;
            error = null;

            if (!IsValidSlotIndex(slotIndex))
            {
                error = $"存档槽编号必须在 0 到 {SlotCount - 1} 之间。";
                return false;
            }

            if (!TryReadSaveFile(slotIndex, out SaveFileData saveFile, out bool exists, out error))
                return false;

            if (!exists)
            {
                error = $"存档槽 {slotIndex} 为空。";
                return false;
            }

            if (!string.IsNullOrEmpty(error))
                return false;

            data = new GameData(saveFile.currentLevelId);
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
                return true;
            }
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is SecurityException)
            {
                error = $"删除存档槽 {slotIndex} 失败：{ex.Message}";
                return false;
            }
        }

        public void Dispose()
        {
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

            if (TrySave(evt.SlotIndex, evt.Data, out string error))
            {
                PublishSlotInfos();
                EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, true, null));
                return;
            }

            EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, false, error));
        }

        private void OnRequestAutoSave(RequestAutoSave evt)
        {
            if (TrySave(0, evt.Data, out string error))
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

        private void PublishSlotInfos()
        {
            EventBus<SaveSlotsRefreshed>.Publish(new SaveSlotsRefreshed(GetSlotInfos()));
        }

        private void OnRequestLoad(RequestLoad evt)
        {
            if (!TryLoad(evt.SlotIndex, out GameData data, out string error))
            {
                EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, false, error));
                return;
            }

            EventBus<SaveLoaded>.Publish(new SaveLoaded(evt.SlotIndex, data));
            EventBus<SaveResult>.Publish(new SaveResult(evt.SlotIndex, true, null));
        }

        private static bool TryReadSaveFile(
            int slotIndex,
            out SaveFileData saveFile,
            out bool exists,
            out string error)
        {
            saveFile = null;
            error = null;

            string filePath = GetSaveFilePath(slotIndex);
            exists = true;
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
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is SecurityException)
            {
                error = $"读取存档槽 {slotIndex} 失败：{ex.Message}";
                return false;
            }

            try
            {
                saveFile = JsonUtility.FromJson<SaveFileData>(json);
            }
            catch (ArgumentException ex)
            {
                error = $"存档槽 {slotIndex} 的 JSON 无法解析：{ex.Message}";
                return true;
            }

            if (saveFile == null)
            {
                error = $"存档槽 {slotIndex} 的 JSON 内容为空或无效。";
                return true;
            }

            if (saveFile.schemaVersion != CurrentSchemaVersion)
            {
                error = $"存档槽 {slotIndex} 的格式版本 {saveFile.schemaVersion} 不受支持。";
                return true;
            }

            if (saveFile.slotIndex != slotIndex)
            {
                error = $"存档文件槽位编号不匹配：预期 {slotIndex}，实际 {saveFile.slotIndex}。";
                return true;
            }

            if (saveFile.currentLevelId <= 0)
            {
                error = $"存档槽 {slotIndex} 的关卡编号无效。";
                return true;
            }

            if (!DateTimeOffset.TryParseExact(
                saveFile.savedAtUtc,
                "O",
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
            catch (Exception ex) when (
                ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is NotSupportedException
                || ex is SecurityException)
            {
                Debug.LogWarning($"无法清理临时存档文件 '{path}'：{ex.Message}");
            }
        }
    }
}
