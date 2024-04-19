using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class TerraFlamebursterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TerraFlameburster";
        public override void SetDefaults() {
            Item.SetCalamitySD<TerraFlameburster>();
            Item.SetCartridgeGun<TerraFlamebursterHeldProj>(160);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
