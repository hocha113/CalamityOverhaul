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
    /// 任务书图谱对外门面。弱引用模组走 <c>Mod.Call</c> 同名命令，本类只给愿意编译引用的模组。<br/>
    /// 命令：<see cref="SupportsExternal"/> / <see cref="RegisterNode"/> / <see cref="AddReward"/> / <see cref="IsCompleted"/> / <see cref="SetObjectiveProgress"/><br/>
    /// 注册须在本模组 Autoload 之后（外模的 PostSetupContent 起）；节点随本模组卸载一并清表，下次加载后要再注册
    /// </summary>
    public static class QuestLogsAPI
    {
        public const string SupportsExternal = "QuestLogs.SupportsExternal";
        public const string RegisterNode = "QuestLogs.RegisterNode";
        public const string AddReward = "QuestLogs.AddReward";
        public const string IsCompleted = "QuestLogs.IsCompleted";
        public const string SetObjectiveProgress = "QuestLogs.SetObjectiveProgress";

        /// <summary>
        /// 按参数表注册运行时节点。ID 碰撞、参数无效、本模组尚未加载完成都返回 false。<br/>
        /// 节点先完整构建再入表，中途失败不会留下半成品
        /// </summary>
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
                if (!QuestNode.AutoloadRegistered) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"QuestLogs not loaded yet, call from PostSetupContent or later (`{id}`)");
                    return false;
                }
                if (QuestNode.GetQuest(id) != null) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"duplicate id `{id}`");
                    return false;
                }

                ExternalCallUtil.TryReadBool(args, false, out bool counts, "countsTowardCompletionist", "counts");
                ExternalCallUtil.TryReadBool(args, false, out bool chapterHub, "chapterHub", "isChapterHub");
                int chapterOrder = 0;
                if (ExternalCallUtil.TryGet(args, out object chapterOrderRaw, "chapterOrder")) {
                    ExternalCallUtil.TryReadInt(chapterOrderRaw, out chapterOrder);
                }
                if (chapterHub && chapterOrder <= 0) {
                    //0 以下与起点争位，教程按第 0 条讲解；枢纽默认排到自有章目之后
                    chapterOrder = 100;
                }
                var node = new ExternalQuestNode(id, counts, chapterHub, chapterOrder);

                ExternalCallUtil.TryGet(args, out object titleRaw, "title", "displayName");
                ExternalCallUtil.TryGet(args, out object summaryRaw, "summary", "description");
                ExternalCallUtil.TryGet(args, out object detailRaw, "detailed", "detailedDescription", "detail");
                ExternalCallUtil.TryGet(args, out object objectiveRaw, "objective", "objectiveText");
                node.BindTexts(
                    ExternalCallUtil.CoerceText(titleRaw),
                    ExternalCallUtil.CoerceText(summaryRaw),
                    ExternalCallUtil.CoerceText(detailRaw),
                    objectiveRaw == null ? null : ExternalCallUtil.CoerceText(objectiveRaw));

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
                int progressMax = 0;
                if (ExternalCallUtil.TryGet(args, out object progressMaxRaw, "progressMax", "requiredProgress")) {
                    ExternalCallUtil.TryReadInt(progressMaxRaw, out progressMax);
                }
                node.BindPredicates(completeBool, completeFloat, unlock, progressMax);

                bool hasPosition = ApplyLayout(node, args);
                if (node.ParentIDs.Count == 0 && !hasPosition && !chapterHub) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"`{id}` has no parents and no position, it will sit on the chart origin over FirstQuest");
                }
                node.VaultSetup();

                if (ExternalCallUtil.TryGet(args, out object rewardsRaw, "rewards")) {
                    foreach (var (item, stack) in ExternalCallUtil.EnumerateRewards(rewardsRaw)) {
                        node.AddReward(item, stack);
                    }
                }

                //全部构建完再入表，失败不留残留
                if (!QuestNode.TryRegisterRuntime(node)) {
                    ExternalCallUtil.LogFailed(RegisterNode, $"register rejected `{id}`");
                    return false;
                }

                //世界内注册补一次进世界检查，但要尊重玩家对本世界的检测决策
                if (!Main.gameMenu && !Main.dedServ && Main.LocalPlayer?.active == true) {
                    var qlPlayer = Main.LocalPlayer.GetModPlayer<QLPlayer>();
                    if (qlPlayer.ShouldCheckQuestInCurrentWorld() && !QuestWorldDecision.IsPending) {
                        node.OnWorldEnter();
                        node.CheckUnlock();
                    }
                }
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(RegisterNode, ex.Message);
                return false;
            }
        }

        /// <summary>给任意已入表节点追加奖励，同物品只保留一条；itemId 或 stack 非正数静默忽略</summary>
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

        /// <summary>只读：未知 ID 不会往玩家档里插键</summary>
        public static bool TryIsCompleted(Player player, string id) {
            try {
                if (player == null || !player.active || string.IsNullOrWhiteSpace(id)) {
                    return false;
                }
                var progress = player.GetModPlayer<QLPlayer>().QuestProgress;
                return progress != null && progress.TryGetValue(id, out var data) && data.IsCompleted;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(IsCompleted, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 手动写外部节点的目标进度（0~1）。只对未绑 <c>complete</c> 谓词的外部节点有意义：
        /// 绑了谓词的节点每帧会用谓词结果覆盖。到 1 后由节点自己的更新走完成流程（弹窗、解锁子节点），
        /// 这里不直接改完成位
        /// </summary>
        public static bool TrySetObjectiveProgress(Player player, string id, float progress01) {
            try {
                player ??= Main.LocalPlayer;
                if (player == null || !player.active || string.IsNullOrWhiteSpace(id)) {
                    ExternalCallUtil.LogFailed(SetObjectiveProgress, "player/id invalid");
                    return false;
                }
                if (QuestNode.GetQuest(id) is not ExternalQuestNode node) {
                    ExternalCallUtil.LogFailed(SetObjectiveProgress, $"`{id}` is not an external node");
                    return false;
                }
                if (node.HasCompletePredicate) {
                    ExternalCallUtil.LogFailed(SetObjectiveProgress, $"`{id}` has a complete predicate, manual progress would be overwritten every frame");
                    return false;
                }
                int required = node.Objectives.Count > 0 ? Math.Max(node.Objectives[0].RequiredProgress, 1) : ExternalQuestNode.DefaultProgressScale;
                var data = player.GetModPlayer<QLPlayer>().GetQuestData(id);
                while (data.ObjectiveProgress.Count < 1) {
                    data.ObjectiveProgress.Add(0);
                }
                data.ObjectiveProgress[0] = (int)(MathHelper.Clamp(progress01, 0f, 1f) * required + 0.001f);
                return true;
            }
            catch (Exception ex) {
                ExternalCallUtil.LogFailed(SetObjectiveProgress, ex.Message);
                return false;
            }
        }

        /// <summary>读坐标、父节点、图标、类型、隐藏；返回调用方是否给了坐标</summary>
        private static bool ApplyLayout(ExternalQuestNode node, IDictionary<string, object> args) {
            float x = 0f, y = 0f;
            bool hasPosition = false;
            if (ExternalCallUtil.TryGet(args, out object posRaw, "position")) {
                if (posRaw is Vector2 vec) {
                    x = vec.X;
                    y = vec.Y;
                    hasPosition = true;
                }
                else if (posRaw is IList list && list.Count >= 2) {
                    hasPosition = ExternalCallUtil.TryReadFloat(list[0], out x) & ExternalCallUtil.TryReadFloat(list[1], out y);
                }
            }
            if (ExternalCallUtil.TryGet(args, out object xRaw, "x")) {
                hasPosition |= ExternalCallUtil.TryReadFloat(xRaw, out x);
            }
            if (ExternalCallUtil.TryGet(args, out object yRaw, "y")) {
                hasPosition |= ExternalCallUtil.TryReadFloat(yRaw, out y);
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
            if (ExternalCallUtil.TryGet(args, out object difficultyRaw, "difficulty")) {
                node.Difficulty = ParseDifficulty(difficultyRaw);
            }

            if (ExternalCallUtil.TryReadBool(args, false, out bool hidden, "hiddenUntilUnlocked")) {
                node.HiddenUntilUnlocked = hidden;
            }
            return hasPosition;
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

        private static QuestDifficulty ParseDifficulty(object raw) {
            if (raw is QuestDifficulty qd) {
                return qd;
            }
            if (raw is string name && Enum.TryParse(name, true, out QuestDifficulty parsed)) {
                return parsed;
            }
            if (ExternalCallUtil.TryReadInt(raw, out int n) && Enum.IsDefined(typeof(QuestDifficulty), n)) {
                return (QuestDifficulty)n;
            }
            return QuestDifficulty.Normal;
        }
    }
}
