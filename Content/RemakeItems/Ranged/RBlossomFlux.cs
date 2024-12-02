using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RBlossomFlux : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BlossomFlux>();
        public override int ProtogenesisID => ModContent.ItemType<BlossomFluxEcType>();
        public override string TargetToolTipItemName => "BlossomFluxEcType";
        public override void SetDefaults(Item item) => BlossomFluxEcType.SetDefaultsFunc(item);
        public override bool? On_CanUseItem(Item item, Player player) {
            return false;
        }
    }
}
