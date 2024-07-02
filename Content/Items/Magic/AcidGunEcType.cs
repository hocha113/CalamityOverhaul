using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class AcidGunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AcidGun";
        public override void SetDefaults() {
            Item.SetCalamitySD<AcidGun>();
            Item.SetHeldProj<AcidGunHeldProj>();
        }
    }
}
