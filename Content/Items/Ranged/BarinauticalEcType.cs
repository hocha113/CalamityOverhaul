using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BarinauticalEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Barinautical";
        public override void SetDefaults() {
            Item.SetCalamitySD<Barinautical>();
            Item.SetHeldProj<BarinauticalHeldProj>();
        }
    }
}
