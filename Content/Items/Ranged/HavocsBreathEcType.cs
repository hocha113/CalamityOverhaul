using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HavocsBreathEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HavocsBreath";
        public override void SetDefaults() {
            Item.SetItemCopySD<HavocsBreath>();
            Item.SetCartridgeGun<HavocsBreathHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
