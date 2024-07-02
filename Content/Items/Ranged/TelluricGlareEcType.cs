using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class TelluricGlareEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TelluricGlare";
        public override void SetDefaults() {
            Item.SetCalamitySD<TelluricGlare>();
            Item.SetHeldProj<TelluricGlareHeldProj>();
        }
    }
}
