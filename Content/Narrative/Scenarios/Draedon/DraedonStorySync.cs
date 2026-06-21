using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Draedon
{
    internal static class DraedonStorySync
    {
        public static DraedonStoryData Story => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<DraedonStoryData>();

        public static bool ReadDraedon(Func<DraedonStoryData, bool> story, Func<DraedonStoryData, bool> legacy) {
            if (story(Story)) {
                return true;
            }

            return legacy(Story);
        }

        public static void WriteDraedon(Action<DraedonStoryData> story, Action<DraedonStoryData> legacy) {
            story(Story);
            legacy(Story);
        }
    }
}
