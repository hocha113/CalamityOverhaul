using CalamityOverhaul.Content.Narrative.Scenarios.Helen;
using CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutFish : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava) {
                return;
            }

            if (HelenEpilogue.SpawnPending) {
                itemDrop = HalibutOverride.ID;
                return;
            }

            if (HalibutState.Read(Player, d => d.HasCaughtHalibut, d => d.HasCaughtHalibut)) {
                if (!Player.HasHalibut() && Main.rand.NextBool(500)) {
                    itemDrop = HalibutOverride.ID;//如果还没有比目鱼，则有较低概率钓到比目鱼
                }
            }
            else {
                if (Main.rand.NextBool(10)) {//如果还没有钓到过比目鱼，则有较高概率钓到比目鱼
                    itemDrop = HalibutOverride.ID;
                }
            }
        }
    }
}
