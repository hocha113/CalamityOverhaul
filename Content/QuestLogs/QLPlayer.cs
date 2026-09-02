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
        /// <summary>信任世界上限，防列表膨胀</summary>
        private const int MaxTrustedQuestWorlds = 32;

        public Dictionary<string, QuestSaveData> QuestProgress = new();

        /// <summary>上次检测的世界全名</summary>
        public string LastWorldFullName = string.Empty;

        /// <summary>本会话跳过检测的世界名</summary>
        public string DontCheckQuestInWorld = string.Empty;

        /// <summary>信任世界列表，写玩家档</summary>
        public List<string> TrustedQuestWorldFullNames = new();

        public override void SaveData(TagCompound tag) {
            try {
                QuestProgress ??= [];
                TagCompound questsTag = new();
                foreach (var kvp in QuestProgress) {
                    questsTag[kvp.Key] = kvp.Value.Serialize();
                }
                tag["QuestProgress"] = questsTag;

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

        public bool ShouldCheckQuestInCurrentWorld() {
            if (DontCheckQuestInWorld == SaveWorld.WorldFullName) {
                return false;
            }
            return true;
        }

        public bool IsCurrentQuestWorldTrusted() {
            if (TrustedQuestWorldFullNames == null || TrustedQuestWorldFullNames.Count == 0) {
                return false;
            }
            string current = SaveWorld.WorldFullName;
            return !string.IsNullOrEmpty(current) && TrustedQuestWorldFullNames.Contains(current);
        }

        public void TrustCurrentQuestWorld() {
            string current = SaveWorld.WorldFullName;
            if (string.IsNullOrEmpty(current)) {
                return;
            }
            TrustedQuestWorldFullNames ??= new List<string>();
            if (TrustedQuestWorldFullNames.Contains(current)) {
                return;
            }
            //超限丢最早
            if (TrustedQuestWorldFullNames.Count >= MaxTrustedQuestWorlds) {
                TrustedQuestWorldFullNames.RemoveAt(0);
            }
            TrustedQuestWorldFullNames.Add(current);
        }

        public void EnableQuestCheckInCurrentWorld(bool runWorldEnterChecks = false) {
            DontCheckQuestInWorld = string.Empty;
            LastWorldFullName = SaveWorld.WorldFullName;

            if (runWorldEnterChecks) {
                RunWorldEnterQuestChecks();
            }
        }

        /// <summary>进世界解锁与刷新</summary>
        public static void RunWorldEnterQuestChecks() {
            if (QuestNode.GetQuest<FirstQuest>() != null) {
                QuestNode.GetQuest<FirstQuest>().IsUnlocked = true;
            }

            //进服查解锁
            foreach (var quest in QuestNode.AllQuests) {
                quest.OnWorldEnter();
                quest.CheckUnlock();
            }
        }

        public override void OnEnterWorld() {
            string currentWorldFullName = SaveWorld.WorldFullName;

            //子世界非跨世界
            bool isSubWorld = SubWorldRef.AnyActiveSubWorld();

            //信任世界静默启用
            if (!isSubWorld && IsCurrentQuestWorldTrusted()) {
                EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);
            }
            //跨世界
            else if (!isSubWorld && !string.IsNullOrEmpty(LastWorldFullName) && LastWorldFullName != currentWorldFullName) {
                DontCheckQuestInWorld = string.Empty;
                QuestWorldDecision.Request(Player);
                return;
            }
            else if (string.IsNullOrEmpty(LastWorldFullName)) {
                //首次进世界
                EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);
            }
            else {
                RunWorldEnterQuestChecks();
            }
        }

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }

            //跳过则不更新
            if (!ShouldCheckQuestInCurrentWorld()) {
                return;
            }

            //决策未答暂停
            if (QuestWorldDecision.IsPending) {
                return;
            }

            //子世界（鬼雨/超梦/旧网等）里不推进目标：探索类目标全按主世界地理判定（Zone*Height 只看纵坐标），
            //在鬼雨世界往下走就会把「到达地狱」做掉（反馈六 #70）；目前没有任何任务的目标写在子世界里，
            //解锁轮询照常，回主世界再续
            bool inSubworld = SubWorldRef.AnyActiveSubWorld();

            //每60帧查未解锁。不再要求开书：隐藏任务靠这里轮询触发条件，
            //普通任务的解锁与通知也不该等到翻书才发生
            bool checkUnlock = Main.GameUpdateCount % 60 == 0;

            foreach (var quest in QuestNode.AllQuests) {
                if (checkUnlock && !quest.IsUnlocked) {
                    quest.CheckUnlock();
                }

                if (!inSubworld && quest.IsUnlocked && !quest.IsCompleted) {
                    quest.UpdateByPlayer();
                }
            }
        }

        public static void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            foreach (var quest in QuestNode.AllQuests) {
                if (quest.IsUnlocked && !quest.IsCompleted) {
                    quest.CraftedItem(recipe, item, consumedItems, destinationStack);
                }
            }
        }
    }
}
