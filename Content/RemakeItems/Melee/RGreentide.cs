using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGreentide : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Greentide>();
        public override int ProtogenesisID => ModContent.ItemType<GreentideEcType>();
        public override string TargetToolTipItemName => "GreentideEcType";
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => GreentideEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return GreentideEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }
}
