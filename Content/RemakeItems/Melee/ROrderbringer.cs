using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class ROrderbringer : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Orderbringer>();
        public override int ProtogenesisID => ModContent.ItemType<OrderbringerEcType>();
        public override string TargetToolTipItemName => "OrderbringerEcType";
        public override void SetDefaults(Item item) => OrderbringerEcType.SetDefaultsFunc(item);
    }
}
