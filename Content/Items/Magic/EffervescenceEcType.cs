using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class EffervescenceEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Effervescence";
        public override void SetDefaults() {
            Item.SetCalamitySD<Effervescence>();
            Item.SetHeldProj<EffervescenceHeldProj>();
        }
    }
}
