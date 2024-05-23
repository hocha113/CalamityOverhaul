using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SandstormGunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SandstormGun";
        public override void SetDefaults() {
            Item.SetCalamitySD<SandstormGun>();
            Item.SetCartridgeGun<SandstormGunHeldProj>(20);
        }
    }
}
