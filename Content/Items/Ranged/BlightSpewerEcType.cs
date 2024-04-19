using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BlightSpewerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BlightSpewer";
        public override void SetDefaults() {
            Item.SetCalamitySD<BlightSpewer>();
            Item.SetCartridgeGun<BlightSpewerHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
