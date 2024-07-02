using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class GoobowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Goobow";
        public override void SetDefaults() {
            Item.SetCalamitySD<Goobow>();
            Item.SetHeldProj<GoobowHeldProj>();
        }
    }
}
