using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
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

            //尾声待兑现且手上确实没鱼时才必出，否则会顶掉全部渔获并能刷出重复的鱼
            if (HelenEpilogue.IsPending(Player) && !Player.HasHalibut()) {
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
