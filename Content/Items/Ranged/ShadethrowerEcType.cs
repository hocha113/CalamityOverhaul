using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ShadethrowerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Shadethrower";
        public override void SetDefaults() {
            Item.SetCalamitySD<Shadethrower>();
            Item.SetCartridgeGun<ShadethrowerHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
