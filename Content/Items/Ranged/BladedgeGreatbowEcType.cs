using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BladedgeGreatbowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BladedgeGreatbow";
        public override void SetDefaults() {
            Item.SetCalamitySD<BladedgeRailbow>();
            Item.SetHeldProj<BladedgeGreatbowHeldProj>();
        }
    }
}
