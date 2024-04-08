using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlurrystormCannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlurrystormCannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlurrystormCannon>();
            Item.SetCartridgeGun<FlurrystormCannonHeldProj>(220);
        }
    }
}
