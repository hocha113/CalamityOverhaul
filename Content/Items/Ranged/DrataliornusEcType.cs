using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class DrataliornusEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Drataliornus";
        public override void SetDefaults() {
            Item.SetCalamitySD<Drataliornus>();
            Item.SetHeldProj<DrataliornusHeldProj>();
        }
    }
}
