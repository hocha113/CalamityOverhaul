using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBlightedCleaver : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BlightedCleaver>();
        public override int ProtogenesisID => ModContent.ItemType<BlightedCleaverEcType>();
        public override string TargetToolTipItemName => "BlightedCleaverEcType";
        public override void SetDefaults(Item item) => BlightedCleaverEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}