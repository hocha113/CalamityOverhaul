using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public class QuestNode
    {
        //节点唯一标识符
        public string ID;
        //节点名称
        public string Name;
        //节点描述
        public string Description;
        //详细任务描述
        public string DetailedDescription;
        //节点在图表中的位置
        public Vector2 Position;
        //前置任务ID列表
        public List<string> ParentIDs = new();
        //子任务ID列表
        public List<string> ChildIDs = new();
        //是否已完成
        public bool IsCompleted;
        //是否已解锁
        public bool IsUnlocked;
        //图标纹理路径
        public string IconTexturePath;
        //任务奖励列表
        public List<QuestReward> Rewards = new();
        //任务目标列表
        public List<QuestObjective> Objectives = new();
        //任务类型
        public QuestType Type;
        //任务难度
        public QuestDifficulty Difficulty;
    }

    //任务奖励结构
    public class QuestReward
    {
        //奖励物品ID
        public int ItemType;
        //奖励数量
        public int Amount;
        //是否已领取
        public bool Claimed;
        //奖励描述
        public string Description;
    }

    //任务目标结构
    public class QuestObjective
    {
        //目标描述
        public string Description;
        //当前进度
        public int CurrentProgress;
        //所需进度
        public int RequiredProgress;
        //是否已完成
        public bool IsCompleted => CurrentProgress >= RequiredProgress;
    }

    //任务类型枚举
    public enum QuestType
    {
        Main,//主线任务
        Side,//支线任务
        Daily,//每日任务
        Achievement//成就任务
    }

    //任务难度枚举
    public enum QuestDifficulty
    {
        Easy,//简单
        Normal,//普通
        Hard,//困难
        Expert,//专家
        Master//大师
    }
}
