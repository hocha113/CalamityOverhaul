using CalamityMod.Items.LoreItems;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal abstract class ModifyLore<T> : ItemOverride where T : ModItem
    {
        public override string LocalizationCategory => "RemakeItems.ModifyLores";
        public override int TargetID => ModContent.ItemType<T>();
        public override bool DrawingInfo => false;
        public LocalizedText Legend {  get; set; }
        public override void PostSetStaticDefaults() => Legend = this.GetLocalization(nameof(Legend));
        public override bool? CanOverride(int id) => id == TargetID;
        public override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRItems.OverModifyTooltip(item, tooltips);
            KeyboardState state = Keyboard.GetState();
            if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                string newContent = Language.GetTextValue($"Mods.CalamityOverhaul.RemakeItems.ModifyLores.{Name}.Legend");
                tooltips.ReplaceTooltip("[legend]", VaultUtils.FormatColorTextMultiLine(newContent, Color.White), "");
            }
            else {
                string newContent = CWRLocText.Instance.Item_LegendOnMouseLang.Value;
                Color newColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                tooltips.ReplaceTooltip("[legend]", VaultUtils.FormatColorTextMultiLine(newContent, newColor), "");
            }
            return false;
        }
    }

    internal class ModifyLoreAbyss : ModifyLore<LoreAbyss> { }

    internal class ModifyLoreBloodMoon : ModifyLore<LoreBloodMoon> { }
}
