using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAbsoluteZero : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AbsoluteZero>();
        public override int ProtogenesisID => ModContent.ItemType<AbsoluteZeroEcType>();
        public override string TargetToolTipItemName => "AbsoluteZeroEcType";
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<AbsoluteZeroHeld>();
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
