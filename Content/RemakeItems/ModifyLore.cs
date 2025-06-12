using CalamityMod.Items.LoreItems;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal abstract class ModifyLore : ItemOverride
    {
        public override string LocalizationCategory => "RemakeItems.ModifyLores";
        public override bool DrawingInfo => false;
        public LocalizedText Legend {  get; set; }
        public override void SetStaticDefaults() => Legend = this.GetLocalization(nameof(Legend));
        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.SetItemLegendContentTops(ref tooltips, Name);
            return false;
        }
    }

    internal class ModifyLoreAbyss : ModifyLore
    {
        public override int TargetID => ModContent.ItemType<LoreAbyss>();
    }
}
