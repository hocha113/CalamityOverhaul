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
        private const string AmountItemKey = "Mods.CalamityOverhaul.QuestLogs.QuestReward.Template.AmountItem";
        private const string SingleToolKey = "Mods.CalamityOverhaul.QuestLogs.QuestReward.Template.SingleTool";

        private static LocalizedText AmountItem => Language.GetOrRegister(AmountItemKey, () => "{0} {1}");
        private static LocalizedText SingleTool => Language.GetOrRegister(SingleToolKey, () => "1 {0}");

        /// <summary>按样式生成奖励描述</summary>
        public static string Format(int itemType, int amount, QuestRewardDescriptionStyle style = QuestRewardDescriptionStyle.AmountItem) {
            if (itemType <= Terraria.ID.ItemID.None || amount <= 0) {
                return string.Empty;
            }

            LocalizedText itemName = VaultUtils.GetLocalizedItemName(itemType);
            if (style == QuestRewardDescriptionStyle.SingleTool) {
                return SingleTool.WithFormatArgs(itemName.Value).Value;
            }

            return AmountItem.WithFormatArgs(amount, itemName.Value).Value;
        }
    }
}
