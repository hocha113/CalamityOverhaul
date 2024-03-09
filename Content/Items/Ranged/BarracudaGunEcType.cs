using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BarracudaGunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BarracudaGun";
        public override void SetDefaults() {
            Item.SetCalamitySD<BarracudaGun>();
            Item.SetHeldProj<BarracudaGunHeldProj>();
        }
    }
}
