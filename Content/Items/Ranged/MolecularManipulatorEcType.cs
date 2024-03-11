using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MolecularManipulatorEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MolecularManipulator";
        public override void SetDefaults() {
            Item.SetCalamitySD<MolecularManipulator>();
            Item.SetCartridgeGun<MolecularManipulatorHeldProj>(480);
        }
    }
}
