using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CondemnationEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Condemnation";
        public override void SetDefaults() {
            Item.SetItemCopySD<Condemnation>();
            Item.SetHeldProj<CondemnationHeldProj>();
        }
    }
}
