using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class ApoctosisArrayEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "ApoctosisArray";
        public override void SetDefaults() {
            Item.SetItemCopySD<ApoctosisArray>();
            Item.SetHeldProj<ApoctosisArrayHeldProj>();
        }
    }
}
