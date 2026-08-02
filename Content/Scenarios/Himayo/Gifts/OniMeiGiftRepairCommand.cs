using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    internal sealed class OniMeiGiftRepairCommand : ModCommand, ILocalizedModType
    {
        public string LocalizationCategory => "Items";
        public override string Command => "cwronimeirepair";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/cwronimeirepair <Key>";
        public override string Description
            => this.GetLocalization(nameof(Description), () => "Requeue one completed Onikiri inscription gift for this character.").Value;

        private LocalizedText LocalOnly
            => this.GetLocalization(nameof(LocalOnly), () => "This command only repairs the current local character.");
        private LocalizedText Success
            => this.GetLocalization(nameof(Success), () => "Onikiri gift requeued: {0}");
        private LocalizedText NotCompleted
            => this.GetLocalization(nameof(NotCompleted), () => "Onikiri gift was never completed: {0}");
        private LocalizedText UnknownKey
            => this.GetLocalization(nameof(UnknownKey), () => "Unknown Onikiri gift Key: {0}");

        public override void Action(CommandCaller caller, string input, string[] args) {
            Player player = caller.Player;
            if (player == null || player.whoAmI != Main.myPlayer || Main.dedServ) {
                caller.Reply(LocalOnly.Value, Color.IndianRed);
                return;
            }
            if (args.Length != 1) {
                caller.Reply(Usage, Color.IndianRed);
                return;
            }
            HimayoGiftRepairResult result = HimayoStorySync.RepairGift(player, args[0], out string canonicalKey);
            switch (result) {
                case HimayoGiftRepairResult.Success:
                    caller.Reply(Success.Format(canonicalKey), Color.LightGreen);
                    break;
                case HimayoGiftRepairResult.NotCompleted:
                    caller.Reply(NotCompleted.Format(canonicalKey), Color.IndianRed);
                    break;
                case HimayoGiftRepairResult.UnknownKey:
                    caller.Reply(UnknownKey.Format(args[0]), Color.IndianRed);
                    break;
                default:
                    caller.Reply(LocalOnly.Value, Color.IndianRed);
                    break;
            }
        }
    }
}
