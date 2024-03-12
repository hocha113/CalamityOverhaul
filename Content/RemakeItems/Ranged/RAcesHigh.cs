using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAcesHigh : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AcesHigh>();
        public override int ProtogenesisID => ModContent.ItemType<AcesHighEcType>();
        public override string TargetToolTipItemName => "AcesHighEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<AcesHighHeldProj>(160);
    }
}
