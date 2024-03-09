using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NorfleetEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Norfleet";
        public override void SetDefaults() {
            Item.SetCalamitySD<Norfleet>();
            Item.SetCartridgeGun<NorfleetHeldProj>(8);
        }
    }
}
