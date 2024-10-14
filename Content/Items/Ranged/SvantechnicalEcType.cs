using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SvantechnicalEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Svantechnical";
        public override void SetDefaults() {
            Item.SetItemCopySD<Svantechnical>();
            Item.SetCartridgeGun<SvantechnicalHeldProj>(280);
            Item.CWR().Scope = true;
        }
    }
}
