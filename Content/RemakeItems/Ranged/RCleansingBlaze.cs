using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCleansingBlaze : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CleansingBlaze>();
        public override int ProtogenesisID => ModContent.ItemType<CleansingBlazeEcType>();
        public override string TargetToolTipItemName => "CleansingBlazeEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<CleansingBlazeHeldProj>(160);
            item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
        }
    }
}
