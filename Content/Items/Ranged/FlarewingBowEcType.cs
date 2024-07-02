using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlarewingBowEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlarewingBow";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlarewingBow>();
            Item.SetHeldProj<FlarewingBowHeldProj>();
        }
    }
}
