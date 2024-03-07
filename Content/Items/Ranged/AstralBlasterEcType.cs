using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AstralBlasterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AstralBlaster";
        public override void SetDefaults() {
            Item.SetCalamityGunSD<AstralBlaster>();
        }
    }
}
