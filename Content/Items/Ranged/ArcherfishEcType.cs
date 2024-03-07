using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ArcherfishEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Archerfish";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<Archerfish>();
            Item.SetCartridgeGun<ArcherfishHeldProj>();
        }
    }
}
