using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AcesHighEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AcesHigh";
        public override void SetDefaults() {
            Item.SetCalamitySD<AcesHigh>();
            Item.SetCartridgeGun<AcesHighHeldProj>(480);
        }
    }
}
