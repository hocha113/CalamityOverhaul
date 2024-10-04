using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RGhastlyVisage : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<GhastlyVisage>();
        public override int ProtogenesisID => ModContent.ItemType<GhastlyVisageEcType>();
        public override string TargetToolTipItemName => "GhastlyVisageEcType";
        public override void SetDefaults(Item item) => GhastlyVisageEcType.SetDefaultsFunc(item);
    }
}
