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
        /// <summary>信任任务世界上限，防列表无限增长</summary>
        private const int MaxTrustedQuestWorlds = 32;

        public Dictionary<string, QuestSaveData> QuestProgress = new();

        /// <summary>上次检测任务的世界完整名称</summary>
        public string LastWorldFullName = string.Empty;

        /// <summary>本会话跳过任务检测的世界名</summary>
        public string DontCheckQuestInWorld = string.Empty;

        /// <summary>信任任务世界列表，持久化到玩家档</summary>
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

                //保存世界追踪
                if (!string.IsNullOrEmpty(LastWorldFullName)) {
                    tag["QL_LastWorldFullName"] = LastWorldFullName;
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

                //加载世界追踪
                LastWorldFullName = string.Empty;
                if (tag.TryGet("QL_LastWorldFullName", out string lastWorld)) {
                    LastWorldFullName = lastWorld;
                }
                //跳过仅本会话
                DontCheckQuestInWorld = string.Empty;
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

        /// <summary>是否应在当前世界检测任务</summary>
        public bool ShouldCheckQuestInCurrentWorld() {
            //如果用户选择跳过
            if (DontCheckQuestInWorld == SaveWorld.WorldFullName) {
                return false;
            }
            return true;
        }

        /// <summary>当前世界是否在信任列表</summary>
        public bool IsCurrentQuestWorldTrusted() {
            if (TrustedQuestWorldFullNames == null || TrustedQuestWorldFullNames.Count == 0) {
                return false;
            }
            string current = SaveWorld.WorldFullName;
            return !string.IsNullOrEmpty(current) && TrustedQuestWorldFullNames.Contains(current);
        }

        /// <summary>将当前世界加入信任列表</summary>
        public void TrustCurrentQuestWorld() {
            string current = SaveWorld.WorldFullName;
            if (string.IsNullOrEmpty(current)) {
                return;
            }
            TrustedQuestWorldFullNames ??= new List<string>();
            if (TrustedQuestWorldFullNames.Contains(current)) {
                return;
            }
            //上限保护，丢弃最早项
            if (TrustedQuestWorldFullNames.Count >= MaxTrustedQuestWorlds) {
                TrustedQuestWorldFullNames.RemoveAt(0);
            }
            TrustedQuestWorldFullNames.Add(current);
        }

        /// <summary>启用当前世界任务检测</summary>
        public void EnableQuestCheckInCurrentWorld(bool runWorldEnterChecks = false) {
            DontCheckQuestInWorld = string.Empty;
            LastWorldFullName = SaveWorld.WorldFullName;

            if (runWorldEnterChecks) {
                RunWorldEnterQuestChecks();
            }
        }

        /// <summary>进世界任务解锁与刷新</summary>
        public static void RunWorldEnterQuestChecks() {
            if (QuestNode.GetQuest<FirstQuest>() != null) {
                QuestNode.GetQuest<FirstQuest>().IsUnlocked = true;
            }

            //进服检查解锁状态
            foreach (var quest in QuestNode.AllQuests) {
                quest.OnWorldEnter();
                quest.CheckUnlock();
            }
        }

        public override void OnEnterWorld() {
            string currentWorldFullName = SaveWorld.WorldFullName;

            //子世界切换不算跨世界
            bool isSubWorld = SubWorldRef.AnyActiveSubWorld();

            //信任世界静默启用检测
            if (!isSubWorld && IsCurrentQuestWorldTrusted()) {
                EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);
            }
            //跨世界进入
            else if (!isSubWorld && !string.IsNullOrEmpty(LastWorldFullName) && LastWorldFullName != currentWorldFullName) {
                DontCheckQuestInWorld = string.Empty;
                QuestWorldDecision.Request(Player);
                return;
            }
            else if (string.IsNullOrEmpty(LastWorldFullName)) {
                //首次进入
                EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);
            }
            else {
                RunWorldEnterQuestChecks();
            }
            //同世界保持选择
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }

            //跳过当前世界则不更新
            if (!ShouldCheckQuestInCurrentWorld()) {
                return;
            }

            //决策未回答时暂停更新
            if (QuestWorldDecision.IsPending) {
                return;
            }

            //每 60 帧检查未解锁
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
            //玩家合成时调用
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.CraftedItem(recipe, item, consumedItems, destinationStack);
                }
            }
        }
    }
}
