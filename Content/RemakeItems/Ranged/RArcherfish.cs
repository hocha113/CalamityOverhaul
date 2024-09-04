using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RArcherfish : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Archerfish>();
        public override int ProtogenesisID => ModContent.ItemType<ArcherfishEcType>();
        public override string TargetToolTipItemName => "ArcherfishEcType";
        public override void SetDefaults(Item item) => item.SetCartridgeGun<ArcherfishHeldProj>(82);
    }
}
