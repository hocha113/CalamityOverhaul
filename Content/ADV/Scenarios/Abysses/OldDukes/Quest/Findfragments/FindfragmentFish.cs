using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments
{
    internal class FindfragmentFish : ModPlayer
    {
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava || !OldDukeCampsite.IsGenerated) {
                return;
            }

            if (Main.rand.NextBool(4)) {
                itemDrop = ModContent.ItemType<Oceanfragments>();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.life <= 0 && target.lifeMax > 100 && Main.rand.NextBool(4) && OldDukeCampsite.IsGenerated && Player.ZoneBeach) {//击杀概率掉落海洋残片
                VaultUtils.SpwanItem(target.FromObjectGetParent(), target.Hitbox, new Item(ModContent.ItemType<Oceanfragments>()));
            }
        }
    }
}
