using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MalevolenceEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Malevolence";
        public override void SetDefaults() {
            Item.SetItemCopySD<Malevolence>();
            Item.SetHeldProj<MalevolenceHeldProj>();
        }
    }
}
