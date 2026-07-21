using Terraria;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    public enum QuestObjectiveDescriptionStyle
    {
        /// <summary>用 <see cref="QuestObjective.Description"/></summary>
        Custom,
        /// <summary>击败{NPC名}</summary>
        DefeatNpc,
        /// <summary>获得{物品名}</summary>
        ObtainItem,
        /// <summary>收集{数量}块{物品名}</summary>
        CollectItem
    }

    internal static class QuestObjectiveTemplates
    {
        public static string Format(QuestObjective objective) {
            switch (objective.DescriptionStyle) {
                case QuestObjectiveDescriptionStyle.DefeatNpc:
                    if (objective.TargetNpcID <= 0) {
                        return string.Empty;
                    }

                    return QuestLog.ObjectiveTemplateDefeatNpc
                        .WithFormatArgs(Lang.GetNPCNameValue(objective.TargetNpcID)).Value;
                case QuestObjectiveDescriptionStyle.ObtainItem:
                    if (objective.TargetItemID <= 0) {
                        return string.Empty;
                    }

                    return QuestLog.ObjectiveTemplateObtainItem
                        .WithFormatArgs(VaultUtils.GetLocalizedItemName(objective.TargetItemID).Value).Value;
                case QuestObjectiveDescriptionStyle.CollectItem:
                    if (objective.TargetItemID <= 0 || objective.RequiredProgress <= 0) {
                        return string.Empty;
                    }

                    return QuestLog.ObjectiveTemplateCollectItem
                        .WithFormatArgs(objective.RequiredProgress, VaultUtils.GetLocalizedItemName(objective.TargetItemID).Value).Value;
                default:
                    return objective.Description?.Value ?? string.Empty;
            }
        }
    }
}
