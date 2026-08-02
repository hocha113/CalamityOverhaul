using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Himayo.Gifts
{
    internal sealed class OniMeiGiftRepairCommand : ModCommand
    {
        public override string Command => "cwronimeirepair";
        public override CommandType Type => CommandType.Chat;
        public override string Usage => "/cwronimeirepair <Key>";
        public override string Description => "Requeue one completed Onikiri inscription gift for this character.";

        public override void Action(CommandCaller caller, string input, string[] args) {
            Player player = caller.Player;
            if (player == null || player.whoAmI != Main.myPlayer || Main.dedServ) {
                caller.Reply("This command only repairs the current local character.", Color.IndianRed);
                return;
            }
            if (args.Length != 1) {
                caller.Reply(Usage, Color.IndianRed);
                return;
            }
            HimayoGiftRepairResult result = HimayoStorySync.RepairGift(player, args[0], out string canonicalKey);
            switch (result) {
                case HimayoGiftRepairResult.Success:
                    caller.Reply($"Onikiri gift requeued: {canonicalKey}", Color.LightGreen);
                    break;
                case HimayoGiftRepairResult.NotCompleted:
                    caller.Reply($"Onikiri gift was never completed: {canonicalKey}", Color.IndianRed);
                    break;
                case HimayoGiftRepairResult.UnknownKey:
                    caller.Reply($"Unknown Onikiri gift Key: {args[0]}", Color.IndianRed);
                    break;
                default:
                    caller.Reply("This command only repairs the current local character.", Color.IndianRed);
                    break;
            }
        }
    }
}
