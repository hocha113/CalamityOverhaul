using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCelestialClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.CelestialClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<CelestialClaymoreEcType>();
        public override string TargetToolTipItemName => "CelestialClaymoreEcType";
        public override void SetDefaults(Item item) {
            CelestialClaymoreEcType.SetDefaultsFunc(item);
            CWRUtils.EasySetLocalTextNameOverride(item, "CelestialClaymoreEcType");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
