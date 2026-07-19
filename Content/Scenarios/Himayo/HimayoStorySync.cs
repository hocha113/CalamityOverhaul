using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Himayo
{
    internal static class HimayoStorySync
    {
        public static HimayoStoryData Story
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HimayoStoryData>();

        public static bool FirstMet => Story.FirstMet;

        public static void MarkFirstMet() => Story.FirstMet = true;

        public static bool ToriiSwordTaken => Story.ToriiSwordTaken;

        public static void MarkToriiSwordTaken() => Story.ToriiSwordTaken = true;
    }
}
