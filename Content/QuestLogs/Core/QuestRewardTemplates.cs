using InnoVault;
using Terraria.Localization;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>任务奖励描述的通用本地化模板</summary>
    internal static class QuestRewardTemplates
    {
        private const string AmountItemKey = "Mods.CalamityOverhaul.QuestLogs.QuestReward.Template.AmountItem";

        private static LocalizedText AmountItem => Language.GetOrRegister(AmountItemKey, () => "{0} {1}");

        /// <summary>按数量与物品名生成奖励描述，如「20 个魔矿」</summary>
        public static string Format(int itemType, int amount) {
            if (itemType <= Terraria.ID.ItemID.None || amount <= 0) {
                return string.Empty;
            }

            LocalizedText itemName = VaultUtils.GetLocalizedItemName(itemType);
            return AmountItem.WithFormatArgs(amount, itemName.Value).Value;
        }
    }
}
