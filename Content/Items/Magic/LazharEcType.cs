using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class LazharEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Lazhar";
        public override void SetDefaults() {
            Item.SetCalamitySD<Lazhar>();
            Item.SetHeldProj<LazharHeldProj>();
        }
    }
}
