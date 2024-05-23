using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class PestilentDefilerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PestilentDefiler";
        public override void SetDefaults() {
            Item.SetCalamitySD<PestilentDefiler>();
            Item.damage = 90;
            Item.SetCartridgeGun<PestilentDefilerHeldProj>(65);
        }
    }
}
