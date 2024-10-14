using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ToxibowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Toxibow";
        public override void SetDefaults() {
            Item.SetItemCopySD<Toxibow>();
            Item.SetHeldProj<ToxibowHeldProj>();
        }
    }
}
