using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDirt : FishSkill
    {
        public override int UnlockFishID => ItemID.Dirtfish;
        public override int DefaultCooldown => 60;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Active(player)) {
                
            }
            return true;//每帧更新
        }
    }
}
