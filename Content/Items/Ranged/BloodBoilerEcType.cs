using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BloodBoilerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BloodBoiler";
        public override void SetDefaults() {
            Item.SetItemCopySD<BloodBoiler>();
            Item.useAmmo = AmmoID.Gel;
            Item.SetCartridgeGun<BloodBoilerHeldProj>(160);
            Item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
