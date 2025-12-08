using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
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
            TagCompound questsTag = new();
            foreach (var kvp in QuestProgress) {
                questsTag[kvp.Key] = kvp.Value.Serialize();
            }
            tag["QuestProgress"] = questsTag;
        }

        public override void LoadData(TagCompound tag) {
            QuestProgress.Clear();
            if (tag.TryGet("QuestProgress", out TagCompound questsTag)) {
                foreach (var kvp in questsTag) {
                    if (kvp.Value is TagCompound questDataTag) {
                        QuestProgress[kvp.Key] = QuestSaveData.Deserialize(questDataTag);
                    }
                }
            }
        }

        public QuestSaveData GetQuestData(string questID) {
            if (!QuestProgress.ContainsKey(questID)) {
                QuestProgress[questID] = QuestSaveData.Default;
            }
            return QuestProgress[questID];
        }

        public override void OnEnterWorld() {
            QuestNode.GetQuest<FirstQuest>().IsUnlocked = true;
        }

        public override void PostUpdate() {
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.OnUpdate();
                }
            }
        }

        public static void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            //玩家合成物品时调用
        }
    }
}
