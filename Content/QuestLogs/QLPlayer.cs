using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.QuestLogs.QLNodes;
using CalamityOverhaul.OtherMods.SubWorld;
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
        /// <summary>
        /// 单个玩家最多记住的"信任任务世界"数量上限，避免列表无限增长
        /// </summary>
        private const int MaxTrustedQuestWorlds = 32;

        public Dictionary<string, QuestSaveData> QuestProgress = new();

        /// <summary>
        /// 上次检测任务的世界完整名称
        /// </summary>
        public string LastWorldFullName = string.Empty;

        /// <summary>
        /// 在此世界中跳过任务检测(用于用户选择跳过后记录)
        /// </summary>
        public string DontCheckQuestInWorld = string.Empty;

        /// <summary>
        /// 玩家选择"信任此世界"的世界完整名字列表
        /// <para>持久化到玩家档；进入这些世界时直接跳过弹窗、自动启用任务检测</para>
        /// </summary>
        public List<string> TrustedQuestWorldFullNames = new();

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
                if (TrustedQuestWorldFullNames != null && TrustedQuestWorldFullNames.Count > 0) {
                    tag["QL_TrustedQuestWorlds"] = TrustedQuestWorldFullNames;
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
                TrustedQuestWorldFullNames = new List<string>();
                if (tag.TryGet("QL_TrustedQuestWorlds", out List<string> trusted) && trusted != null) {
                    foreach (var w in trusted) {
                        if (!string.IsNullOrEmpty(w) && !TrustedQuestWorldFullNames.Contains(w)) {
                            TrustedQuestWorldFullNames.Add(w);
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

        /// <summary>
        /// 当前世界是否在玩家的"信任任务世界"列表里
        /// </summary>
        public bool IsCurrentQuestWorldTrusted() {
            if (TrustedQuestWorldFullNames == null || TrustedQuestWorldFullNames.Count == 0) {
                return false;
            }
            string current = SaveWorld.WorldFullName;
            return !string.IsNullOrEmpty(current) && TrustedQuestWorldFullNames.Contains(current);
        }

        /// <summary>
        /// 把当前世界加入"信任任务世界"列表
        /// </summary>
        public void TrustCurrentQuestWorld() {
            string current = SaveWorld.WorldFullName;
            if (string.IsNullOrEmpty(current)) {
                return;
            }
            TrustedQuestWorldFullNames ??= new List<string>();
            if (TrustedQuestWorldFullNames.Contains(current)) {
                return;
            }
            //上限保护：超出时丢弃最早的一个
            if (TrustedQuestWorldFullNames.Count >= MaxTrustedQuestWorlds) {
                TrustedQuestWorldFullNames.RemoveAt(0);
            }
            TrustedQuestWorldFullNames.Add(current);
        }

        public override void OnEnterWorld() {
            string currentWorldFullName = SaveWorld.WorldFullName;

            //子世界切换不视为跨世界，避免频繁弹出确认窗口
            bool isSubWorld = SubWorldRef.AnyActiveSubWorld();

            //信任世界：直接静默启用任务检测，跳过任何弹窗逻辑
            //这样玩家在多个常驻世界之间跳转时，再也不会被问到任务检测的事
            if (!isSubWorld && IsCurrentQuestWorldTrusted()) {
                DontCheckQuestInWorld = string.Empty;
                LastWorldFullName = currentWorldFullName;
            }
            //检测是否进入了不同的世界
            else if (!isSubWorld && !string.IsNullOrEmpty(LastWorldFullName) && LastWorldFullName != currentWorldFullName) {
                //进入了不同的世界，重置跳过标记并弹出确认窗口
                DontCheckQuestInWorld = string.Empty;
                QuestWorldConfirmUI.RequestConfirm(Player, Main.worldName, LastWorldFullName);
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
