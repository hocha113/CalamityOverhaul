using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class EidolicWailEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "EidolicWail";
        public override void SetDefaults() {
            Item.SetCalamitySD<EidolicWail>();
            Item.useTime = 95;
            Item.damage = 285;
            Item.mana = 52;
            Item.SetHeldProj<EidolicWailHeldProj>();
        }
    }
}
