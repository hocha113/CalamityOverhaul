using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSuperradiantSlaughterer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SuperradiantSlaughterer>();
        public override bool DrawingInfo => false;
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {//重新补上修改，因为描述替换会让原本的修改操作失效
            tooltips.ReplaceTooltip("[MAIN]"
                , VaultUtils.FormatColorTextMultiLine(this.GetLocalizedValue("MainInfo")
                , new Color(180, 255, 0)), "");
            tooltips.ReplaceTooltip("[ALT]"
                , VaultUtils.FormatColorTextMultiLine(this.GetLocalization("AltInfo")
                .Format(SuperradiantSlaughterer.DashCooldown / 60)
                , new Color(120, 255, 120)), "");
        }
        public override void ModifyRecipe(Recipe recipe) {
            recipe.RemoveIngredient(ModContent.ItemType<SpeedBlaster>());
        }
    }
}
