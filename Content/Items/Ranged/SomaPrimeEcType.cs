using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SomaPrimeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SomaPrime";
        public override void SetDefaults() {
            Item.SetCalamitySD<SomaPrime>();
            Item.SetCartridgeGun<SomaPrimeHeldProj>(600);
        }
    }
}
