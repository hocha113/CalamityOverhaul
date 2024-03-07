using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ArcNovaDiffuserEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArcNovaDiffuser";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<ArcNovaDiffuser>();
            Item.SetCartridgeGun<ArcNovaDiffuserHeldProj>();
        }
    }
}
