using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBloodBoiler : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BloodBoiler>();
        public override int ProtogenesisID => ModContent.ItemType<BloodBoilerEcType>();
        public override string TargetToolTipItemName => "BloodBoilerEcType";
        public override void SetDefaults(Item item) {
            item.useAmmo = AmmoID.Gel;
            item.SetCartridgeGun<BloodBoilerHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
