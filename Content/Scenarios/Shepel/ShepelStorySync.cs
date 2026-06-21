using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using InnoVault.Narrative.Runtime;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    internal static class ShepelStorySync
    {
        public static ShepelStoryData Story => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<ShepelStoryData>();

        public static bool ReadShepel(Func<ShepelStoryData, bool> story, Func<ShepelStoryData, bool> legacy) {
            if (story(Story)) {
                return true;
            }

            return legacy(Story);
        }

        public static void WriteShepel(Action<ShepelStoryData> story, Action<ShepelStoryData> legacy) {
            story(Story);
            legacy(Story);
        }

        public static ShepelGiftStoryData GiftStory => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<ShepelGiftStoryData>();

        public static bool ReadGift(Func<ShepelGiftStoryData, bool> story, Func<ShepelGiftStoryData, bool> legacy) {
            if (story(GiftStory)) {
                return true;
            }

            return legacy(GiftStory);
        }

        public static void WriteGift(Action<ShepelGiftStoryData> story, Action<ShepelGiftStoryData> legacy) {
            story(GiftStory);
            legacy(GiftStory);
        }

        public static int TakeVariantSeed(
            Func<ShepelStoryData, int> readStory,
            Action<ShepelStoryData, int> writeStory,
            Func<ShepelStoryData, int> readLegacy,
            Action<ShepelStoryData, int> writeLegacy,
            int modulus) {
            int seed = readStory(Story);
            int legacy = readLegacy(Story);
            if (legacy > seed) {
                seed = legacy;
            }

            int variant = seed % modulus;
            int next = seed + 1;
            WriteShepel(d => writeStory(d, next), d => writeLegacy(d, next));
            return variant;
        }

        public static void MarkFirstSHPCIntroCompleted()
            => WriteShepel(d => d.FirstSHPCIntroCompleted = true, d => d.FirstSHPCIntroCompleted = true);

        public static bool CanStartSHPCTrialQuests(Player player) {
            if (player == null || !player.active || !player.HasItem(SHPCOverride.ID)) {
                return false;
            }

            if (ReadShepel(d => d.FirstSHPCIntroCompleted, d => d.FirstSHPCIntroCompleted)) {
                return true;
            }

            if (ReadShepel(d => d.FirstSHPCObtained, d => d.FirstSHPCObtained)
                && !NarrativeTriggerGate.IsBusy) {
                MarkFirstSHPCIntroCompleted();
                return true;
            }

            return false;
        }
    }
}
