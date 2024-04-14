using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RGoobow : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Goobow>();
        public override int ProtogenesisID => ModContent.ItemType<GoobowEcType>();
        public override string TargetToolTipItemName => "GoobowEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<GoobowHeldProj>();
    }
}
