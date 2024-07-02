using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ArterialAssaultEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ArterialAssault";
        public override void SetDefaults() {
            Item.SetCalamitySD<ArterialAssault>();
            Item.SetHeldProj<ArterialAssaultHeldProj>();
        }
    }
}
