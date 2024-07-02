using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AcesHighEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AcesHigh";
        public override void SetDefaults() {
            Item.SetCalamitySD<AcesHigh>();
            Item.damage = 375;
            Item.SetCartridgeGun<AcesHighHeldProj>(90);
        }
    }
}
