using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class BrimstoneFuryEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BrimstoneFury";
        public override void SetDefaults() {
            Item.SetCalamitySD<BrimstoneFury>();
            Item.SetHeldProj<BrimstoneFuryHeldProj>();
        }
    }
}
