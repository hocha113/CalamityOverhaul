using System;
using System.Collections;
using System.Collections.Generic;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.Localization;

namespace CalamityOverhaul.API
{
    /// <summary>
    /// 任务书图谱对外门面。弱引用模组走 <c>Mod.Call</c> 同名命令，本类只给愿意编译引用的模组。
    /// 命令：<see cref="SupportsExternal"/> / <see cref="RegisterNode"/> / <see cref="AddReward"/> / <see cref="IsCompleted"/> / <see cref="SetObjectiveProgress"/>
    /// </summary>
    public static class QuestLogsAPI
    {
        public const string SupportsExternal = "QuestLogs.SupportsExternal";
        public const string RegisterNode = "QuestLogs.RegisterNode";
        public const string AddReward = "QuestLogs.AddReward";
        public const string IsCompleted = "QuestLogs.IsCompleted";
        public const string SetObjectiveProgress = "QuestLogs.SetObjectiveProgress";

        /// <summary>按参数表注册运行时节点。ID 碰撞或参数无效返回 false</summary>
        public static bool TryRegisterNode(IDictionary<string, object> args) {
            try {
                if (args == null) {
                    ExternalCallUtil.LogFailed(RegisterNode, "args null");
                    return false;
                }

                string id = ExternalCallUtil.ReadString(args, "id");
                if (string.IsNullOrWhiteSpace(id)) {
                    ExternalCallUtil.LogFailed(RegisterNode, "id empty");
                    return false;
                }
                if (QuestNode.GetQuest(id) != null) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"duplicate id `{id}`");
                    return false;
                }

                ExternalCallUtil.TryReadBool(args, false, out bool counts, "countsTowardCompletionist", "counts");
                var node = new ExternalQuestNode(id, counts);

                ExternalCallUtil.TryGet(args, out object titleRaw, "title", "displayName");
                ExternalCallUtil.TryGet(args, out object summaryRaw, "summary", "description");
                ExternalCallUtil.TryGet(args, out object detailRaw, "detailed", "detailedDescription", "detail");
                node.BindTexts(
                    ExternalCallUtil.CoerceText(titleRaw, id, "Title"),
                    ExternalCallUtil.CoerceText(summaryRaw, id, "Summary"),
                    ExternalCallUtil.CoerceText(detailRaw, id, "Detail"));

                Func<Player, bool> completeBool = null;
                Func<Player, float> completeFloat = null;
                Func<Player, bool> unlock = null;
                if (ExternalCallUtil.TryGet(args, out object completeRaw, "complete", "isComplete")
                    && !ExternalCallUtil.TryBindComplete(completeRaw, out completeBool, out completeFloat)) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"`complete` is not Func<Player,bool|float> (`{id}`)");
                }
                if (ExternalCallUtil.TryGet(args, out object unlockRaw, "unlock", "canUnlock")
                    && !ExternalCallUtil.TryBindComplete(unlockRaw, out unlock, out _)) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"`unlock` is not Func<Player,bool> (`{id}`)");
                    unlock = null;
                }
                node.BindPredicates(completeBool, completeFloat, unlock);

                if (!QuestNode.TryRegisterRuntime(node)) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"register rejected `{id}`");
                    return false;
                }

                ApplyLayout(node, args);
                node.VaultSetup();

                if (ExternalCallUtil.TryGet(args, out object rewardsRaw, "rewards")) {
                    foreach (var (item, stack) in ExternalCallUtil.EnumerateRewards(rewardsRaw)) {
                        node.AddReward(item, stack);
                    }
                }

                if (!Main.gameMenu && Main.LocalPlayer?.active == true) {
                    node.OnWorldEnter();
                    node.CheckUnlock();
                }
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(RegisterNode, ex.Message);
                return false;
            }
        }

        public static bool TryAddReward(string id, int itemId, int stack = 1) {
            try {
                var node = QuestNode.GetQuest(id);
                if (node == null) {
                    ExternalCallUtil.LogFailed(AddReward, $"missing `{id}`");
                    return false;
                }
                if (itemId <= 0 || stack <= 0) {
                    return true;
                }
                node.AddReward(itemId, stack);
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(AddReward, ex.Message);
                return false;
            }
        }

        public static bool TryIsCompleted(Player player, string id) {
            try {
                if (player == null || !player.active || string.IsNullOrWhiteSpace(id)) {
                    return false;
                }
                return player.GetModPlayer<QLPlayer>().GetQuestData(id).IsCompleted;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(IsCompleted, ex.Message);
                return false;
            }
        }

        public static bool TrySetObjectiveProgress(Player player, string id, float progress01) {
            try {
                player ??= Main.LocalPlayer;
                if (player == null || !player.active || string.IsNullOrWhiteSpace(id)) {
                    ExternalCallUtil.LogFailed(SetObjectiveProgress, "player/id invalid");
                    return false;
                }
                var data = player.GetModPlayer<QLPlayer>().GetQuestData(id);
                int required = ExternalQuestNode.ProgressScale;
                var node = QuestNode.GetQuest(id);
                if (node != null && node.Objectives.Count > 0) {
                    required = Math.Max(node.Objectives[0].RequiredProgress, 1);
                }
                while (data.ObjectiveProgress.Count < 1) {
                    data.ObjectiveProgress.Add(0);
                }
                data.ObjectiveProgress[0] = (int)(MathHelper.Clamp(progress01, 0f, 1f) * required);
                if (progress01 >= 1f) {
                    data.IsCompleted = true;
                }
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetObjectiveProgress, ex.Message);
                return false;
            }
        }

        private static void ApplyLayout(ExternalQuestNode node, IDictionary<string, object> args) {
            float x = 0f, y = 0f;
            if (ExternalCallUtil.TryGet(args, out object posRaw, "position")) {
                if (posRaw is Vector2 vec) {
                    x = vec.X;
                    y = vec.Y;
                }
                else if (posRaw is IList list && list.Count >= 2) {
                    ExternalCallUtil.TryReadFloat(list[0], out x);
                    ExternalCallUtil.TryReadFloat(list[1], out y);
                }
            }
            if (ExternalCallUtil.TryGet(args, out object xRaw, "x")) {
                ExternalCallUtil.TryReadFloat(xRaw, out x);
            }
            if (ExternalCallUtil.TryGet(args, out object yRaw, "y")) {
                ExternalCallUtil.TryReadFloat(yRaw, out y);
            }
            node.Position = new Vector2(x, y);

            if (ExternalCallUtil.TryGet(args, out object parentsRaw, "parents", "parent")) {
                foreach (string parentId in EnumerateIds(parentsRaw)) {
                    node.AddParent(parentId);
                }
            }

            if (ExternalCallUtil.TryGet(args, out object iconItemRaw, "iconItem", "itemIcon")
                && ExternalCallUtil.TryReadInt(iconItemRaw, out int iconItem) && iconItem > 0) {
                node.SetItemIcon(iconItem);
            }
            else if (ExternalCallUtil.TryGet(args, out object iconNpcRaw, "iconNpc", "npcIcon")
                && ExternalCallUtil.TryReadInt(iconNpcRaw, out int iconNpc) && iconNpc > 0) {
                node.SetNPCIcon(iconNpc);
            }
            else {
                string icon = ExternalCallUtil.ReadString(args, "icon", "iconPath");
                if (!string.IsNullOrWhiteSpace(icon)) {
                    node.SetTextureIcon(icon);
                }
            }

            if (ExternalCallUtil.TryGet(args, out object typeRaw, "type", "questType")) {
                node.QuestType = ParseQuestType(typeRaw);
            }

            if (ExternalCallUtil.TryReadBool(args, false, out bool hidden, "hiddenUntilUnlocked")) {
                node.HiddenUntilUnlocked = hidden;
            }
        }

        private static IEnumerable<string> EnumerateIds(object raw) {
            if (raw is string one && !string.IsNullOrWhiteSpace(one)) {
                yield return one;
                yield break;
            }
            if (raw is IEnumerable seq && raw is not string) {
                foreach (object item in seq) {
                    string id = Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(id)) {
                        yield return id;
                    }
                }
            }
        }

        private static QuestType ParseQuestType(object raw) {
            if (raw is QuestType qt) {
                return qt;
            }
            if (raw is string name && Enum.TryParse(name, true, out QuestType parsed)) {
                return parsed;
            }
            if (ExternalCallUtil.TryReadInt(raw, out int n) && Enum.IsDefined(typeof(QuestType), n)) {
                return (QuestType)n;
            }
            return QuestType.Side;
        }
    }
}
