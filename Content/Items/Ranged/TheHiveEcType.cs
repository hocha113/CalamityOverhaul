using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class TheHiveEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TheHive";
        public override void SetDefaults() {
            Item.SetCalamitySD<TheHive>();
            Item.SetCartridgeGun<TheHiveHeldProj>(28);
        }
    }
}
