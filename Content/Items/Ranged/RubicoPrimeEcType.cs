using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class RubicoPrimeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "RubicoPrime";
        public override void SetDefaults() {
            Item.SetItemCopySD<RubicoPrime>();
            Item.damage = 820;
            Item.useTime = 20;
            Item.SetCartridgeGun<RubicoPrimeHeldProj>(80);
        }
    }
}
