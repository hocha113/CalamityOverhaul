using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.OtherMods.ImproveGame;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.OldDuke.Quest
{
    internal sealed class FindFragmentQuestEntry : EntrustEntryData
    {
        private const int TargetCount = 777;

        public LocalizedText ObjectiveFormat { get; init; }
        public LocalizedText CollectFormat { get; init; }
        public LocalizedText CurrentFormat { get; init; }
        public LocalizedText ReturnFormat { get; init; }
        public LocalizedText QuestCompleteFormat { get; init; }
        public LocalizedText HintFormat { get; init; }

        private int fragmentCount;

        public FindFragmentQuestEntry(string key, LocalizedText title, LocalizedText summary, LocalizedText category)
            : base(key, title, summary, category) { }

        public override float GetTrackerContentTopPadding() => 5f;

        public override void OnUpdate() {
            if (Status == QuestEntryStatus.Completed || Status == QuestEntryStatus.Failed) {
                return;
            }

            fragmentCount = GetFragmentCount();
            Progress = MathHelper.Clamp(fragmentCount / (float)TargetCount, 0f, 1f);
        }

        public override List<string> GetTrackerDetails() {
            if (OldDukeStorySync.Read(d => d.OldDukeFindFragmentsQuestCompleted, d => d.OldDukeFindFragmentsQuestCompleted)) {
                return [QuestCompleteFormat?.Value ?? "Quest Complete!"];
            }

            List<string> lines = [];
            lines.Add($"{ObjectiveFormat?.Value ?? ""}: {CollectFormat?.Value ?? ""}");
            lines.Add($"{CurrentFormat?.Value ?? ""}: {fragmentCount}/{TargetCount}");

            if (fragmentCount >= TargetCount) {
                lines.Add($"> {ReturnFormat?.Value ?? ""} <");
            }
            else {
                lines.Add(HintFormat?.Value ?? "");
            }

            return lines;
        }

        public static int GetFragmentCount() {
            int count = 0;
            Player player = Main.LocalPlayer;
            int fragmentType = ModContent.ItemType<Oceanfragments>();

            var bigBags = player.GetBigBagItems() ?? [];
            Item[][] inventories = [
                player.inventory,
                player.bank.item,
                player.bank2.item,
                player.bank3.item,
                player.bank4.item,
                [.. bigBags],
            ];

            foreach (Item[] inventory in inventories) {
                for (int i = 0; i < inventory.Length; i++) {
                    if (inventory[i].type == fragmentType) {
                        count += inventory[i].stack;
                    }
                }
            }

            return count;
        }
    }
}
