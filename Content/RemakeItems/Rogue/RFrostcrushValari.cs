using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Rogue
{
    internal class RFrostcrushValari : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<FrostcrushValari>();
 
        public override void SetDefaults(Item item) => item.shoot = ModContent.ProjectileType<FrostcrushValariHeld>();
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 6;
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
