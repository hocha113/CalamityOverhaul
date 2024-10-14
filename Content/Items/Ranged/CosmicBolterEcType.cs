using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CosmicBolterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "VernalBolter";
        public override void SetDefaults() {
            Item.SetItemCopySD<VernalBolter>();
            Item.SetHeldProj<CosmicBolterHeldProj>();
        }
    }
}
