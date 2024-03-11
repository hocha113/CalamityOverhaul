using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class OnyxiaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Onyxia";
        public override void SetDefaults() {
            Item.SetCalamitySD<Onyxia>();
            Item.SetCartridgeGun<OnyxiaHeldProj>(480);
        }
    }
}
