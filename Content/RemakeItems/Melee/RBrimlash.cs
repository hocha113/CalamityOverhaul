using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrimlash : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Brimlash>();
        public override int ProtogenesisID => ModContent.ItemType<BrimlashEcType>();
        public override string TargetToolTipItemName => "BrimlashEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetDefaults(Item item) => BrimlashEcType.SetDefaultsFunc(item);
        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.useTime = item.useAnimation = 30;
            if (player.altFunctionUse == 2) {
                item.useTime = item.useAnimation = 22;
            }
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) 
            => BrimlashEcType.ShootFunc(item, player, source, position, velocity, type, damage, knockback);
    }
}
