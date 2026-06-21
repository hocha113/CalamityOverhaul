using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Helen
{
    internal static class HalibutState
    {
        public static bool Read(Player player, Func<HalibutStoryData, bool> story, Func<HalibutStoryData, bool> legacy) {
            if (player?.active == true && story(player.GetModPlayer<StoryPlayer>().Get<HalibutStoryData>())) {
                return true;
            }

            return player?.active == true && legacy(player.GetModPlayer<StoryPlayer>().Get<HalibutStoryData>());
        }

        public static void Write(Player player, Action<HalibutStoryData> story, Action<HalibutStoryData> legacy) {
            if (player == null) {
                return;
            }

            HalibutStoryData data = player.GetModPlayer<StoryPlayer>().Get<HalibutStoryData>();
            story(data);
            legacy(data);
        }

        public static bool ReadGift(Player player, Func<BossGiftStoryData, bool> story, Func<BossGiftStoryData, bool> legacy) {
            if (player?.active == true && story(player.GetModPlayer<StoryPlayer>().Get<BossGiftStoryData>())) {
                return true;
            }

            return player?.active == true && legacy(player.GetModPlayer<StoryPlayer>().Get<BossGiftStoryData>());
        }

        public static void WriteGift(Player player, Action<BossGiftStoryData> story, Action<BossGiftStoryData> legacy) {
            if (player == null) {
                return;
            }

            BossGiftStoryData data = player.GetModPlayer<StoryPlayer>().Get<BossGiftStoryData>();
            story(data);
            legacy(data);
        }
    }
}
