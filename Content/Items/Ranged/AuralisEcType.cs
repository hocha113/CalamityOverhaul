using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AuralisEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Auralis";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<Auralis>();
            Item.SetCartridgeGun<AuralisHeldProj>(18);
        }
    }
}
