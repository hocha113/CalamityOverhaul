using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments
{
    internal class FindfragmentFish : ModPlayer
    {
        /// <summary>
        /// 是否可以尝试关于海洋残片的任务钓鱼
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool CanAttemptQuestFishing(Player player) {
            if (player == null || !player.active) {
                return false;
            }

            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            //只有在任务触发后且未完成前显示
            if (!save.OldDukeFindFragmentsQuestTriggered) {
                return false;
            }

            if (save.OldDukeFindFragmentsQuestCompleted) {
                return false;
            }

            return true;
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (attempt.inHoney || attempt.inLava || !CanAttemptQuestFishing(Player)) {
                return;
            }

            if (Main.rand.NextBool(10)) {
                itemDrop = ModContent.ItemType<Oceanfragments>();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.life <= 0 && target.lifeMax > 100 && Main.rand.NextBool(16) && CanAttemptQuestFishing(Player) && Player.ZoneBeach) {//击杀概率掉落海洋残片
                VaultUtils.SpwanItem(target.FromObjectGetParent(), target.Hitbox, new Item(ModContent.ItemType<Oceanfragments>()));
            }
        }
    }
}
