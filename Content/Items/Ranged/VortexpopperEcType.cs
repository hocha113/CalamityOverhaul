using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class VortexpopperEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Vortexpopper";
        public override void SetDefaults() {
            Item.SetItemCopySD<Vortexpopper>();
            Item.SetCartridgeGun<VortexpopperHeldProj>(85);
        }
    }
}
