using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTerraFlameburster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TerraFlameburster>();
        public override int ProtogenesisID => ModContent.ItemType<TerraFlamebursterEcType>();
        public override string TargetToolTipItemName => "TerraFlamebursterEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<TerraFlamebursterHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
