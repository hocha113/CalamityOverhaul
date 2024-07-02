using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HoarfrostBowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HoarfrostBow";
        public override void SetDefaults() {
            Item.SetCalamitySD<HoarfrostBow>();
            Item.SetHeldProj<HoarfrostBowHeldProj>();
        }
    }
}
