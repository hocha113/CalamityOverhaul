using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NitroExpressRifleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NitroExpressRifle";
        public override void SetDefaults() {
            Item.SetCalamitySD<NitroExpressRifle>();
            Item.SetCartridgeGun<NitroExpressRifleHeldProj>(8);
        }
    }
}
