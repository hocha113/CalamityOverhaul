using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
using InnoVault.GameSystem;
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

        /// <summary>
        /// 上次检测任务的世界完整名称
        /// </summary>
        public string LastWorldFullName = string.Empty;

        /// <summary>
        /// 在此世界中跳过任务检测(用于用户选择跳过后记录)
        /// </summary>
        public string DontCheckQuestInWorld = string.Empty;

        public override bool IsLoadingEnabled(Mod mod) => CWRServerConfig.Instance.QuestLog;

        public override void SaveData(TagCompound tag) {
            try {
                QuestProgress ??= [];
                TagCompound questsTag = new();
                foreach (var kvp in QuestProgress) {
                    questsTag[kvp.Key] = kvp.Value.Serialize();
                }
                tag["QuestProgress"] = questsTag;

                //保存世界追踪数据
                if (!string.IsNullOrEmpty(LastWorldFullName)) {
                    tag["QL_LastWorldFullName"] = LastWorldFullName;
                }
                if (!string.IsNullOrEmpty(DontCheckQuestInWorld)) {
                    tag["QL_DontCheckQuestInWorld"] = DontCheckQuestInWorld;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[QLPlayer:SaveData] an error has occurred:{ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                QuestProgress ??= [];
                QuestProgress.Clear();
                if (tag.TryGet("QuestProgress", out TagCompound questsTag)) {
                    foreach (var kvp in questsTag) {
                        if (kvp.Value is TagCompound questDataTag) {
                            QuestProgress[kvp.Key] = QuestSaveData.Deserialize(questDataTag);
                        }
                    }
                }

                //加载世界追踪数据
                LastWorldFullName = string.Empty;
                if (tag.TryGet("QL_LastWorldFullName", out string lastWorld)) {
                    LastWorldFullName = lastWorld;
                }
                DontCheckQuestInWorld = string.Empty;
                if (tag.TryGet("QL_DontCheckQuestInWorld", out string dontCheck)) {
                    DontCheckQuestInWorld = dontCheck;
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

        /// <summary>
        /// 检查是否应该在当前世界检测任务
        /// </summary>
        public bool ShouldCheckQuestInCurrentWorld() {
            //如果用户选择了跳过当前世界的任务检测
            if (DontCheckQuestInWorld == SaveWorld.WorldFullName) {
                return false;
            }
            return true;
        }

        public override void OnEnterWorld() {
            string currentWorldFullName = SaveWorld.WorldFullName;

            //每次进入世界都重置跳过标记，确保每次进入都会提醒
            //只有当用户在本次会话中选择跳过后才会设置DontCheckQuestInWorld
            //这样下次进入世界时会重新询问

            //检测是否进入了不同的世界
            if (!string.IsNullOrEmpty(LastWorldFullName) && LastWorldFullName != currentWorldFullName) {
                //进入了不同的世界，重置跳过标记并弹出确认窗口
                DontCheckQuestInWorld = string.Empty;
                QuestWorldConfirmUI.RequestConfirm(Main.worldName, LastWorldFullName);
            }
            else if (string.IsNullOrEmpty(LastWorldFullName)) {
                //首次进入，正常设置
                LastWorldFullName = currentWorldFullName;
            }
            //同一世界不需要重置，保持之前的选择

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

            //如果用户跳过了当前世界的任务检测，则不更新任务
            if (!ShouldCheckQuestInCurrentWorld()) {
                return;
            }

            //如果确认窗口正在显示，暂停任务更新
            if (QuestWorldConfirmUI.Instance != null && QuestWorldConfirmUI.Instance.Active) {
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
