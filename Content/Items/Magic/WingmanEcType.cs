using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class WingmanEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Wingman";
        public override void SetDefaults() {
            Item.SetItemCopySD<Wingman>();
            Item.SetHeldProj<WingmanHeldProj>();
        }
    }
}
