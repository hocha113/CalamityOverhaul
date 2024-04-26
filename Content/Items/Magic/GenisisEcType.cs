using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class GenisisEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Genisis";
        public override void SetDefaults() {
            Item.SetCalamitySD<Genisis>();
            Item.SetHeldProj<GenisisHeldProj>();
        }
    }
}
