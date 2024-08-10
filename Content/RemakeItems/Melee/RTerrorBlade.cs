using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTerrorBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TerrorBlade>();
        public override int ProtogenesisID => ModContent.ItemType<TerrorBladeEcType>();
        public override string TargetToolTipItemName => "TerrorBladeEcType";
        public override void SetDefaults(Item item) => TerrorBladeEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
