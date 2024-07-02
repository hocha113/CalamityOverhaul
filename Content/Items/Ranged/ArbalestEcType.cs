using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ArbalestEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Arbalest";
        public override void SetDefaults() {
            Item.SetCalamitySD<Arbalest>();
            Item.SetHeldProj<ArbalestHeldProj>();
            Item.CWR().Scope = true;
        }
    }
}
