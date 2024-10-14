using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NitroExpressRifleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NitroExpressRifle";
        public override void SetDefaults() {
            Item.SetItemCopySD<NitroExpressRifle>();
            Item.SetCartridgeGun<NitroExpressRifleHeldProj>(8);
        }
    }
}
