using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    internal sealed class HelenEpilogue : NarrativeScenario, ILocalizedModType
    {
        public static bool SpawnPending;

        public string LocalizationCategory => "ADV.EternalBlazingNow";

        public static LocalizedText EpilogueLine1 { get; private set; }
        public static LocalizedText EpilogueLine2 { get; private set; }
        public static LocalizedText EpilogueLine3 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override void SetStaticDefaults() {
            EpilogueLine1 = this.GetLocalization(nameof(EpilogueLine1), () => "我在等一个笨蛋");
            EpilogueLine2 = this.GetLocalization(nameof(EpilogueLine2), () => ".....");
            EpilogueLine3 = this.GetLocalization(nameof(EpilogueLine3), () => "欢迎回来.....");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say("Helen", "Silence", EpilogueLine1.Value)
             .Say("Helen", "Silence", EpilogueLine2.Value)
             .Say("Helen", EpilogueLine3.Value);
        }

        public static void RequestSpawn() => SpawnPending = true;

        public static void ResetWorldState() => SpawnPending = false;
    }

    internal sealed class HelenEpilogueNPC : GlobalNPC
    {
        public override void OnKill(NPC npc) {
            if (npc.type != CWRID.NPC_PrimordialWyrmHead || !HelenEpilogue.SpawnPending) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player.HasItem(HalibutOverride.ID)) {
                return;
            }

            int insertIdx = player.selectedItem is >= 0 and < 10 ? player.selectedItem : 0;
            const int inventoryEnd = 50;

            int emptyIdx = -1;
            for (int i = insertIdx; i < inventoryEnd; i++) {
                if (player.inventory[i].IsAir) {
                    emptyIdx = i;
                    break;
                }
            }

            if (emptyIdx == -1) {
                int dropIdx = -1;
                int lowestValue = int.MaxValue;
                for (int i = insertIdx; i < inventoryEnd; i++) {
                    if (!player.inventory[i].IsAir && player.inventory[i].value <= lowestValue) {
                        lowestValue = player.inventory[i].value;
                        dropIdx = i;
                    }
                }

                if (dropIdx == -1) {
                    return;
                }

                int droppedType = player.inventory[dropIdx].type;
                int droppedStack = player.inventory[dropIdx].stack;
                int droppedPrefix = player.inventory[dropIdx].prefix;
                player.inventory[dropIdx].TurnToAir();
                Item.NewItem(player.GetSource_DropAsItem(), player.Hitbox, droppedType, droppedStack, false, droppedPrefix);
                emptyIdx = dropIdx;
            }

            for (int i = emptyIdx; i > insertIdx; i--) {
                player.inventory[i] = player.inventory[i - 1];
            }

            player.inventory[insertIdx] = new Item();
            player.inventory[insertIdx].SetDefaults(HalibutOverride.ID);
        }
    }
}
