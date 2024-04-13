using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AstrealDefeatEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstrealDefeat";
        public override void SetDefaults() {
            Item.SetCalamitySD<AstrealDefeat>();
            Item.SetHeldProj<AstrealDefeatHeldProj>();
        }
    }
}
