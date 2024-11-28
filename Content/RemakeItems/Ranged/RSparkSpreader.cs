using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSparkSpreader : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SparkSpreader>();
        public override int ProtogenesisID => ModContent.ItemType<SparkSpreaderEcType>();
        public override string TargetToolTipItemName => "SparkSpreaderEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SparkSpreaderHeldProj>(120);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
