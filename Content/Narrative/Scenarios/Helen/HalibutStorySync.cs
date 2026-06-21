using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Helen
{
    internal static class HalibutStorySync
    {
        public static HalibutStoryData Story => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<HalibutStoryData>();

        public static bool ReadHalibut(Func<HalibutStoryData, bool> story, Func<HalibutStoryData, bool> legacy) {
            if (story(Story)) {
                return true;
            }

            return legacy(Story);
        }

        public static void WriteHalibut(Action<HalibutStoryData> story, Action<HalibutStoryData> legacy) {
            story(Story);
            legacy(Story);
        }

        public static BossGiftStoryData GiftStory => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<BossGiftStoryData>();

        public static bool ReadGift(Func<BossGiftStoryData, bool> story, Func<BossGiftStoryData, bool> legacy) {
            if (story(GiftStory)) {
                return true;
            }

            return legacy(GiftStory);
        }

        public static void WriteGift(Action<BossGiftStoryData> story, Action<BossGiftStoryData> legacy) {
            story(GiftStory);
            legacy(GiftStory);
        }

        public static SupCalStoryData SupCalStory => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<SupCalStoryData>();

        public static bool ReadSupCal(Func<SupCalStoryData, bool> story, Func<SupCalStoryData, bool> legacy) {
            if (story(SupCalStory)) {
                return true;
            }

            return legacy(SupCalStory);
        }

        public static void WriteSupCal(Action<SupCalStoryData> story, Action<SupCalStoryData> legacy) {
            story(SupCalStory);
            legacy(SupCalStory);
        }
    }
}
