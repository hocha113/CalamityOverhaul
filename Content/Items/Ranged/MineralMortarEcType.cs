using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MineralMortarEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MineralMortar";
        public override void SetDefaults() {
            Item.SetCalamitySD<MineralMortar>();
            Item.SetCartridgeGun<MineralMortarHeldProj>(480);
        }
    }
}
