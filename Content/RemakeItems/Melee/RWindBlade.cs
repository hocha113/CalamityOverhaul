using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RWindBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<WindBlade>();
        public override int ProtogenesisID => ModContent.ItemType<WindBladeEcType>();
        public override string TargetToolTipItemName => "WindBladeEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<WindBlade>()] = true;
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetDefaults(Item item) => WindBladeEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return WindBladeEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }
}
