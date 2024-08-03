using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RForbiddenOathblade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<ForbiddenOathblade>();
        public override int ProtogenesisID => ModContent.ItemType<ForbiddenOathbladeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<ForbiddenOathbladeHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
