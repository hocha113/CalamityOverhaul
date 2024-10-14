using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BarinadeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinade";
        public override void SetDefaults() {
            Item.SetItemCopySD<Barinade>();
            Item.SetHeldProj<BarinadeHeldProj>();
        }
    }
}
