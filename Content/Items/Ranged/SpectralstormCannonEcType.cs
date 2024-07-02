using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SpectralstormCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SpectralstormCannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<SpectralstormCannon>();
            Item.SetCartridgeGun<SpectralstormCannonHeldProj>(180);
        }
    }
}
