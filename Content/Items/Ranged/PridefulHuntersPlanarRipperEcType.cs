using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class PridefulHuntersPlanarRipperEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PridefulHuntersPlanarRipper";
        public override void SetDefaults() {
            Item.SetCalamitySD<PridefulHuntersPlanarRipper>();
            Item.damage = 45;
            Item.SetCartridgeGun<PridefulHuntersPlanarRipperHeldProj>(280);
        }
    }
}
