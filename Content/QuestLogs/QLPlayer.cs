using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.QuestLogs
{
    internal class QLPlayer : ModPlayer
    {
        public Dictionary<string, QuestSaveData> QuestProgress = new();

        public override void SaveData(TagCompound tag) {
            try {
                TagCompound questsTag = new();
                foreach (var kvp in QuestProgress) {
                    questsTag[kvp.Key] = kvp.Value.Serialize();
                }
                tag["QuestProgress"] = questsTag;
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[QLPlayer:SaveData] an error has occurred:{ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                QuestProgress.Clear();
                if (tag.TryGet("QuestProgress", out TagCompound questsTag)) {
                    foreach (var kvp in questsTag) {
                        if (kvp.Value is TagCompound questDataTag) {
                            QuestProgress[kvp.Key] = QuestSaveData.Deserialize(questDataTag);
                        }
                    }
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[QLPlayer:LoadData] an error has occurred:{ex.Message}");
            }
        }

        public QuestSaveData GetQuestData(string questID) {
            if (!QuestProgress.ContainsKey(questID)) {
                QuestProgress[questID] = QuestSaveData.Default;
            }
            return QuestProgress[questID];
        }

        public override void OnEnterWorld() {
            if (QuestNode.GetQuest<FirstQuest>() != null) {
                QuestNode.GetQuest<FirstQuest>().IsUnlocked = true;
            }

            //进服时检查一遍所有任务的解锁状态，防止因更新或存档问题导致的任务未解锁
            foreach (var quest in QuestNode.AllQuests) {
                quest.OnWorldEnter();
                quest.CheckUnlock();
            }
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }

            //每60帧检查一次未解锁的任务，防止漏掉
            bool checkUnlock = Main.GameUpdateCount % 60 == 0 && QuestLog.Instance.visible;

            foreach (var quest in QuestNode.AllQuests) {
                if (checkUnlock && !quest.IsUnlocked) {
                    quest.CheckUnlock();
                }

                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.UpdateByPlayer();
                }
            }
        }

        public static void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            //玩家合成物品时调用
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.CraftedItem(recipe, item, consumedItems, destinationStack);
                }
            }
        }
    }
}
