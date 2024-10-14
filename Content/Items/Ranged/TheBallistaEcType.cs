using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class TheBallistaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheBallista";
        public override void SetDefaults() {
            Item.SetItemCopySD<TheBallista>();
            Item.SetHeldProj<TheBallistaHeldProj>();
        }
    }
}
