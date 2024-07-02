using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SparkSpreaderEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SparkSpreader";
        public override void SetDefaults() {
            Item.SetCalamitySD<SparkSpreader>();
            Item.SetCartridgeGun<SparkSpreaderHeldProj>(120);
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
