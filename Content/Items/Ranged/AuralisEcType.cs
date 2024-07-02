using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AuralisEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Auralis";
        public override void SetDefaults() {
            Item.SetCalamitySD<Auralis>();
            Item.SetCartridgeGun<AuralisHeldProj>(18);
            Item.CWR().Scope = true;
        }
    }
}
