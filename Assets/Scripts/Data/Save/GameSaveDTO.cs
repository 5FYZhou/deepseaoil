using System;
using System.Collections.Generic;

namespace DeepseaOil.Data
{
    /// <summary>存档头：槽位与时间，读整档之前先只读这一段</summary>
    /// <remarks>字段一律 public 且只由值类型与 string 构成，JsonUtility 不认属性、字典与接口。</remarks>
    [Serializable]
    public class SaveHeaderDTO
    {
        public int schemaVersion;

        public int slotIndex;

        public bool isAuto;

        public int currentLevelId;

        public int waveIndex;

        /// <summary>UTC ISO-8601（"O"），与 SaveService 的解析格式成对</summary>
        public string savedAtUtc;

        public string title;

        public string timeText;
    }

    /// <summary>玩家池子与位置</summary>
    [Serializable]
    public class PlayerSaveData
    {
        public float posX;

        public float posY;

        public float faceX;

        public float faceY;

        public float hp;

        public int water;

        public int earth;

        public int seedType;

        public int seedCount;
    }

    /// <summary>波次快照；出怪序列不存，按 cursor 重展开</summary>
    [Serializable]
    public class WaveSaveData
    {
        public int waveIndex;

        public int cursor;

        public int pendingCursor;

        public int phase;

        public float timer;

        public int pendingSeed;

        public float spawnTimer;

        public bool isCompleted;

        public bool settleDelayStarted;
    }

    /// <summary>一只活体敌人：只存补残血要用的两段</summary>
    [Serializable]
    public class EnemySaveData
    {
        public int enemyId;

        public float posX;

        public float posY;

        public int hp;
    }

    /// <summary>一格地貌；remaining -1 = 无倒计时</summary>
    [Serializable]
    public class TileSaveData
    {
        public int cellX;

        public int cellY;

        public int stateId;

        public float remaining;
    }

    /// <summary>一件在场掉落物；只存落点</summary>
    [Serializable]
    public class DropSaveData
    {
        public int type;

        public int seed;

        public float posX;

        public float posY;
    }

    /// <summary>关卡是否已开战；波次数值在 WaveSaveData，这一项决定读档后该不该立刻跑波次</summary>
    [Serializable]
    public class LevelSaveData
    {
        public int state;

        public bool victoryEndEventReleased;
    }

    /// <summary>剧情进度：段号 + 行下标；已播标记与队列也要存，否则读档后同一段会被再次触发</summary>
    [Serializable]
    public class StorySaveData
    {
        public int stage;

        public int paragraph;

        public int lineIndex;

        public bool seedDialogueQueued;

        public bool levelFourWaveDialogueQueued;

        public bool levelFiveSunriseDialogueQueued;

        public bool levelFiveDuskDialogueQueued;

        public bool endingDialogueQueued;

        public List<int> pendingParagraphs;
    }

    /// <summary>整档根；schemaVersion 放最外层</summary>
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion;

        public SaveHeaderDTO header;

        public PlayerSaveData player;

        public WaveSaveData wave;

        public LevelSaveData level;

        public List<EnemySaveData> enemies;

        public List<TileSaveData> tiles;

        public List<DropSaveData> drops;

        public StorySaveData story;
    }
}
