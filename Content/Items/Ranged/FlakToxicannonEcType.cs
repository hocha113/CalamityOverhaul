using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FlakToxicannonEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "FlakToxicannon";
        public override void SetDefaults() {
            Item.SetCalamitySD<FlakToxicannon>();
            Item.SetCartridgeGun<FlakToxicannonHeldProj>(160);
        }
    }
}
