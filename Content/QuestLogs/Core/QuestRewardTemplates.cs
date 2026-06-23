using CalamityOverhaul.Content.QuestLogs;
using InnoVault;
using Terraria.Localization;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>奖励描述的自动生成样式</summary>
    public enum QuestRewardDescriptionStyle
    {
        /// <summary>{数量} {物品名}</summary>
        AmountItem,
        /// <summary>单件工具/武器，如「一把铁镐」</summary>
        SingleTool
    }

    /// <summary>任务奖励描述的通用本地化模板</summary>
    internal static class QuestRewardTemplates
    {
        /// <summary>按样式生成奖励描述</summary>
        public static string Format(int itemType, int amount, QuestRewardDescriptionStyle style = QuestRewardDescriptionStyle.AmountItem) {
            if (itemType <= Terraria.ID.ItemID.None || amount <= 0) {
                return string.Empty;
            }

            LocalizedText itemName = VaultUtils.GetLocalizedItemName(itemType);
            if (style == QuestRewardDescriptionStyle.SingleTool) {
                return QuestLog.RewardTemplateSingleTool.WithFormatArgs(itemName.Value).Value;
            }

            return QuestLog.RewardTemplateAmountItem.WithFormatArgs(amount, itemName.Value).Value;
        }
    }
}
