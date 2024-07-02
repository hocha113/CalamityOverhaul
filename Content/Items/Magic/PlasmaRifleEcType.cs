using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class PlasmaRifleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "PlasmaRifle";
        public override void SetDefaults() {
            Item.SetCalamitySD<PlasmaRifle>();
            Item.SetHeldProj<PlasmaRifleHeldProj>();
        }
    }
}
