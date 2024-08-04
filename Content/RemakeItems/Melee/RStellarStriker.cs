using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStellarStriker : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.StellarStriker>();
        public override int ProtogenesisID => ModContent.ItemType<StellarStrikerEcType>();
        public override string TargetToolTipItemName => "StellarStrikerEcType";
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => StellarStrikerEcType.SetDefaultsFunc(item);
        public override bool? UseItem(Item item, Player player) => StellarStrikerEcType.UseItemFunc(item, player);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return StellarStrikerEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }
}
