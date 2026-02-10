using CalamityOverhaul.Common;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows.EternalBlazingNow;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutFish : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava) {
                return;
            }

            if (HelenEpilogue.Spwan) {
                itemDrop = HalibutOverride.ID;
                return;
            }

            if (Player.TryGetADVSave(out var save) && save.HasCaughtHalibut) {
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
