using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    internal class SHPCModTooltipDraw : GlobalItem, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        public static LocalizedText InstalledHeader { get; private set; }
        public static LocalizedText NoModules { get; private set; }
        public static LocalizedText BonusHeader { get; private set; }

        public override void SetStaticDefaults() {
            InstalledHeader = this.GetLocalization(nameof(InstalledHeader));
            NoModules = this.GetLocalization(nameof(NoModules));
            BonusHeader = this.GetLocalization(nameof(BonusHeader));
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (item.type != SHPCOverride.ID || lines.Count == 0) return;

            Player player = Main.LocalPlayer;
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();

            var modules = new List<(Item m, SHPCModuleItem mod)>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                Item m = sp.GetModule(i);
                if (m != null && !m.IsAir && m.ModItem is SHPCModuleItem mod)
                    modules.Add((m, mod));
            }

            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            var bonusLines = new List<(string text, bool isNeg)>();
            foreach (string s in SHPCModuleItem.BuildStatLines(ctx)) {
                if (!string.IsNullOrEmpty(s))
                    bonusLines.Add((s, s.StartsWith("-")));
            }

            if (modules.Count == 0 && bonusLines.Count == 0) return;

            int tipLeft = int.MaxValue, tipTop = int.MaxValue, tipRight = 0;
            foreach (var l in lines) {
                Vector2 sz = l.Font.MeasureString(l.Text) * l.BaseScale;
                tipLeft = Math.Min(tipLeft, l.X);
                tipTop = Math.Min(tipTop, l.Y);
                tipRight = Math.Max(tipRight, l.X + (int)sz.X);
            }

            var font = FontAssets.MouseText.Value;
            const float TextScale = 0.85f;
            const float HeaderTextScale = TextScale * 1.2f;
            const int Padding = 8;
            const int IconSize = 16;
            const int IconTextGap = 4;
            const int LineH = 20;
            const int PanelGap = 6;

            float maxW = font.MeasureString(InstalledHeader.Value).X * HeaderTextScale;
            if (bonusLines.Count > 0)
                maxW = MathF.Max(maxW, font.MeasureString(BonusHeader.Value).X * HeaderTextScale);
            foreach (var (m, _) in modules)
                maxW = MathF.Max(maxW, (IconSize + IconTextGap + font.MeasureString(m.Name).X) * TextScale);
            foreach (var (s, _) in bonusLines)
                maxW = MathF.Max(maxW, font.MeasureString(s).X * TextScale);

            int lineCount = 1 + modules.Count + (bonusLines.Count > 0 ? 1 + bonusLines.Count : 0);
            int panelW = (int)(maxW) + Padding * 2;
            int panelH = Padding * 2 + lineCount * LineH + 10;

            int panelX = tipRight + PanelGap + 10;
            int panelY = tipTop;
            if (panelX + panelW > Main.screenWidth - 4)
                panelX = tipLeft - PanelGap - panelW;
            panelY = Math.Clamp(panelY, 4, Math.Max(4, Main.screenHeight - panelH - 4));

            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            sb.Draw(pixel, new Rectangle(panelX - 1, panelY - 1, panelW + 2, panelH + 2), new Color(0, 180, 220, 160));
            sb.Draw(pixel, new Rectangle(panelX, panelY, panelW, panelH), new Color(12, 18, 38, 230));

            float tx = panelX + Padding;
            float ty = panelY + Padding;

            Utils.DrawBorderString(sb, InstalledHeader.Value, new Vector2(tx, ty), new Color(0, 220, 255), HeaderTextScale);
            ty += LineH * 1.2f;

            foreach (var (m, mod) in modules) {
                Vector2 iconCenter = new(tx + IconSize * 0.5f, ty + LineH * 0.5f);
                SHPCModuleRender.DrawIcon(sb, m, iconCenter, IconSize, mod.TintColor, 1f, Main.UIScaleMatrix, mod.TintIntensity);
                Utils.DrawBorderString(sb, m.Name, new Vector2(tx + IconSize + IconTextGap, ty + 2), mod.TintColor, TextScale);
                ty += LineH;
            }

            ty += 10;
            if (bonusLines.Count > 0) {
                Utils.DrawBorderString(sb, BonusHeader.Value, new Vector2(tx, ty), new Color(0, 220, 255), HeaderTextScale);
                ty += LineH * 1.2f;
                foreach (var (s, isNeg) in bonusLines) {
                    Color c = isNeg ? new Color(255, 120, 110) : new Color(120, 255, 170);
                    Utils.DrawBorderString(sb, s, new Vector2(tx, ty), c, TextScale);
                    ty += LineH;
                }
            }
        }
    }
}
