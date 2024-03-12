using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class OnyxChainBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "OnyxChainBlaster";
        public override void SetDefaults() {
            Item.SetCalamitySD<OnyxChainBlaster>();
            Item.SetCartridgeGun<OnyxChainBlasterHeldProj>(120);
        }
    }
}
