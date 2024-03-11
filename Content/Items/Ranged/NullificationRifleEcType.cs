using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NullificationRifleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "NullificationRifle";
        public override void SetDefaults() {
            Item.SetCalamitySD<NullificationRifle>();
            Item.SetCartridgeGun<NullificationRifleHeldProj>(480);
        }
    }
}
