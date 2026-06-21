using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    internal sealed class OldDukeNarrativeTicker : ModSystem
    {
        public override void OnWorldLoad() {
            CampsiteChatDialogue.ResetWorldState();
        }
    }
}
