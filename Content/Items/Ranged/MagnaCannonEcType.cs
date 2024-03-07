using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MagnaCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MagnaCannon";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<MagnaCannon>();
            Item.SetCartridgeGun<MagnaCannonHeldProj>();
        }
    }
}
