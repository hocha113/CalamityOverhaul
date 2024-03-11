using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SepticSkewerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SepticSkewer";
        public override void SetDefaults() {
            Item.SetCalamitySD<SepticSkewer>();
            Item.SetCartridgeGun<SepticSkewerHeldProj>(8);
        }
    }
}
