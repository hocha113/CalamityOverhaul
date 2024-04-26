using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class IonBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "IonBlaster";
        public override void SetDefaults() {
            Item.SetCalamitySD<IonBlaster>();
            Item.SetHeldProj<IonBlasterHeldProj>();
        }
    }
}
