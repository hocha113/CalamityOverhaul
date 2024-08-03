using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheLastMourning : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TheLastMourning>();
        public override int ProtogenesisID => ModContent.ItemType<TheLastMourningEcType>();
        public override string TargetToolTipItemName => "TheLastMourningEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => TheLastMourningEcType.SetDefaultsFunc(item);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return TheLastMourningEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }
}
