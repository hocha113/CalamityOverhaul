using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPristineFury : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<PristineFury>();
        public override int ProtogenesisID => ModContent.ItemType<PristineFuryEcType>();
        public override string TargetToolTipItemName => "PristineFuryEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<PristineFuryHeldProj>(160);
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
        }
    }
}
