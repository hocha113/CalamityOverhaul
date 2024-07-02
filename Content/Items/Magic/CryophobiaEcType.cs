using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class CryophobiaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "Cryophobia";
        public override void SetDefaults() {
            Item.SetCalamitySD<Cryophobia>();
            Item.SetHeldProj<CryophobiaHeldProj>();
        }
    }
}
