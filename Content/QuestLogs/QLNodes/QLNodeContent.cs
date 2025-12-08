using CalamityOverhaul.Content.QuestLogs.Core;
using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class QLNodeContent
    {
        public static void Setup() {
            QuestLog.Instance.Nodes.Clear();

            //初始化一些测试节点
            QuestLog.Add(new QuestNode {
                ID = "1",
                Name = "启程",
                Description = "开始你的旅程",
                DetailedDescription = "欢迎来到灾厄全知领域！这是你的第一个任务，完成它来熟悉系统。",
                Position = new Vector2(0, 0),
                IsUnlocked = true,
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Easy,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "探索世界", CurrentProgress = 0, RequiredProgress = 1 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Wood, Amount = 50, Description = "木材奖励" }
                }
            });

            QuestLog.Add(new QuestNode {
                ID = "2",
                Name = "挖掘",
                Description = "获得第一块矿石",
                DetailedDescription = "挖掘是生存的基础，去地下寻找有用的矿石吧。",
                Position = new Vector2(150, 0),
                ParentIDs = new List<string> { "1" },
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Easy,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "获得铜矿", CurrentProgress = 0, RequiredProgress = 10 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.CopperPickaxe, Amount = 1, Description = "铜镐奖励" }
                }
            });

            QuestLog.Add(new QuestNode {
                ID = "3",
                Name = "制作",
                Description = "制作工作台",
                DetailedDescription = "工作台是所有制作的基础，学会如何制作它。",
                Position = new Vector2(300, 80),
                ParentIDs = new List<string> { "2" },
                Type = QuestType.Side,
                Difficulty = QuestDifficulty.Normal,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "制作工作台", CurrentProgress = 0, RequiredProgress = 1 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Wood, Amount = 100, Description = "木材奖励" }
                }
            });

            QuestLog.Add(new QuestNode {
                ID = "4",
                Name = "战斗",
                Description = "击败史莱姆",
                DetailedDescription = "学习战斗技巧，击败你的第一个敌人。",
                Position = new Vector2(300, -80),
                ParentIDs = new List<string> { "2" },
                Type = QuestType.Main,
                Difficulty = QuestDifficulty.Normal,
                Objectives = new List<QuestObjective> {
                    new QuestObjective { Description = "击败史莱姆", CurrentProgress = 0, RequiredProgress = 5 }
                },
                Rewards = new List<QuestReward> {
                    new QuestReward { ItemType = ItemID.Gel, Amount = 20, Description = "凝胶奖励" }
                }
            });
        }
    }
}
