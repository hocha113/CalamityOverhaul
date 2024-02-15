using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    internal class ItemSystem : ModSystem
    {
        public static void SetItemLegendContentTops(ref List<TooltipLine> tooltips, string itemKey) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[legend]") && x.Mod == "Terraria");
            if (legendtops != null) {
                KeyboardState state = Keyboard.GetState();
                if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                    legendtops.Text = Language.GetTextValue($"Mods.CalamityOverhaul.Items.{itemKey}.Legend");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
                else {
                    legendtops.Text = CWRLocalizationText.GetTextValue("Item_LegendOnMouseLang");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.Gold, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
            }
        }

        public override void PostSetupContent() {
            if (ContentConfig.Instance.ReplaceAsset) {
                TextureAssets.Item[ModContent.ItemType<GeliticBlade>()] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Replace/GeliticBlade");
                TextureAssets.Item[ModContent.ItemType<Goobow>()] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Replace/Goobow");
                TextureAssets.Item[ModContent.ItemType<GunkShot>()] = CWRUtils.GetT2DAsset(CWRConstant.Item + "Replace/GunkShot");
            }
        }
    }
}
