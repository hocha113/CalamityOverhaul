using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ClamorRifleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ClamorRifle";
        public override void SetDefaults() {
            Item.SetCalamitySD<ClamorRifle>();
            Item.SetCartridgeGun<ClamorRifleHeldProj>(45);
        }
    }
}
