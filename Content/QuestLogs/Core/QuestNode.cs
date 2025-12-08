using System.Collections.Generic;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public class QuestNode
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string ID;
        /// <summary>
        /// 节点名称
        /// </summary>
        public string Name;
        /// <summary>
        /// 节点描述
        /// </summary>
        public string Description;
        /// <summary>
        /// 详细任务描述
        /// </summary>
        public string DetailedDescription;
        /// <summary>
        /// 节点在图表中的位置
        /// </summary>
        public Vector2 Position;
        /// <summary>
        /// 前置任务ID列表
        /// </summary>
        public List<string> ParentIDs = new();
        /// <summary>
        /// 子任务ID列表
        /// </summary>
        public List<string> ChildIDs = new();
        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted;
        /// <summary>
        /// 是否已解锁
        /// </summary>
        public bool IsUnlocked;
        /// <summary>
        /// 图标纹理路径
        /// </summary>
        public string IconTexturePath;
        /// <summary>
        /// 任务奖励列表
        /// </summary>
        public List<QuestReward> Rewards = new();
        /// <summary>
        /// 任务目标列表
        /// </summary>
        public List<QuestObjective> Objectives = new();
        /// <summary>
        /// 任务类型
        /// </summary>
        public QuestType Type;
        /// <summary>
        /// 任务难度
        /// </summary>
        public QuestDifficulty Difficulty;
    }

    /// <summary>
    /// 任务奖励结构
    /// </summary>
    public class QuestReward
    {
        /// <summary>
        /// 奖励物品ID
        /// </summary>
        public int ItemType;
        /// <summary>
        /// 奖励数量
        /// </summary>
        public int Amount;
        /// <summary>
        /// 是否已领取
        /// </summary>
        public bool Claimed;
        /// <summary>
        /// 奖励描述
        /// </summary>
        public string Description;
    }

    /// <summary>
    /// 任务目标结构
    /// </summary>
    public class QuestObjective
    {
        /// <summary>
        /// 目标描述
        /// </summary>
        public string Description;
        /// <summary>
        /// 当前进度
        /// </summary>
        public int CurrentProgress;
        /// <summary>
        /// 所需进度
        /// </summary>
        public int RequiredProgress;
        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted => CurrentProgress >= RequiredProgress;
    }

    /// <summary>
    /// 任务类型枚举
    /// </summary>
    public enum QuestType
    {
        Main,//主线任务
        Side,//支线任务
        Daily,//每日任务
        Achievement//成就任务
    }

    /// <summary>
    /// 任务难度枚举
    /// </summary>
    public enum QuestDifficulty
    {
        Easy,//简单
        Normal,//普通
        Hard,//困难
        Expert,//专家
        Master//大师
    }
}
