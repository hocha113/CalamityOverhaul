using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPhosphorescentGauntlet : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PhosphorescentGauntlet>();
        public override int ProtogenesisID => ModContent.ItemType<PhosphorescentGauntletEcType>();
        public override string TargetToolTipItemName => "PhosphorescentGauntletEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => item.damage = 1205;
        public override void HoldItem(Item item, Player player) => PhosphorescentGauntletEcType.HoldItemFunc(player);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? On_CanUseItem(Item item, Player player) => PhosphorescentGauntletEcType.CanUseItemFunc(player, item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => PhosphorescentGauntletEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
    }
}
