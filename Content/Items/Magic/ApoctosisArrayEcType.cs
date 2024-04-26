using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class ApoctosisArrayEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "ApoctosisArray";
        public override void SetDefaults() {
            Item.SetCalamitySD<ApoctosisArray>();
            Item.SetHeldProj<ApoctosisArrayHeldProj>();
        }
    }
}
