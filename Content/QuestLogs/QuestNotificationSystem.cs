using CalamityOverhaul.Content.QuestLogs.Core;
using CalamityOverhaul.Content.UIs.NotificationPopup;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>任务完成通知装饰器</summary>
    public class QuestNotificationSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText Text1;

        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "任务完成");
        }

        public static void AddNotification(QuestNode node) {
            NotificationPopupSystem.Add(new QuestCompletionEntry(node));
        }
    }
}
