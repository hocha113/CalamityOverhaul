using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>
    /// 模具加工台左侧 6 类别按钮：色条 / 名称 / 碎片计数 / 图鉴进度
    /// </summary>
    internal static class MoldCategorySidebar
    {
        private const int CategoryCount = SHPCData.SlotCount;

        //当前帧悬停的类别索引（-1 表示无）
        private static int hoverIdx = -1;

        public static int HoverIndex => hoverIdx;

        public static Rectangle GetRowRect(in MoldLayout layout, int idx) {
            int rowH = (int)MoldLayout.SidebarRowH;
            int gap = (int)MoldLayout.SidebarRowGap;
            return new Rectangle(
                layout.Sidebar.X,
                layout.Sidebar.Y + idx * (rowH + gap),
                layout.Sidebar.Width,
                rowH);
        }

        public static void UpdateHover(in MoldLayout layout, Vector2 mouse, MoldProcessingUI ui) {
            hoverIdx = -1;
            for (int i = 0; i < CategoryCount; i++) {
                if (GetRowRect(layout, i).Contains((int)mouse.X, (int)mouse.Y)) {
                    hoverIdx = i;
                    return;
                }
            }
        }

        public static bool HandleClick(MoldProcessingUI ui) {
            if (hoverIdx < 0 || hoverIdx >= CategoryCount) {
                return false;
            }
            if (ui.SelectedCategory != (SHPCSlotCategory)hoverIdx) {
                ui.SelectedCategory = (SHPCSlotCategory)hoverIdx;
                MoldProcessingPanel.ScrollReset();
                MoldCodexPanel.ScrollReset();
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            return true;
        }

        public static void Draw(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            in MoldLayout layout, MoldProcessingUI ui, float a) {
            Player p = Main.LocalPlayer;
            SHPCPlayer sp = p != null ? SHPCPlayer.Get(p) : null;

            for (int i = 0; i < CategoryCount; i++) {
                SHPCSlotCategory cat = (SHPCSlotCategory)i;
                Rectangle r = GetRowRect(layout, i);
                bool isHover = hoverIdx == i;
                bool isActive = ui.SelectedCategory == cat;

                //投影
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height),
                    new Color(0, 0, 0) * (0.4f * a));

                Color bg = isActive ? new Color(12, 50, 70) * (0.95f * a)
                    : isHover ? new Color(8, 30, 44) * (0.9f * a)
                    : new Color(6, 20, 30) * (0.85f * a);
                SHPCRenderer.DrawFilledRect(sb, px, r, bg);

                //类别色条（左侧 4px）
                Color band = SHPCModuleItem.SlotCategoryColor(cat);
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle(r.X, r.Y, 4, r.Height),
                    band * (isActive ? 1f * a : 0.7f * a));

                //描边
                Color border = isActive ? SHPCTheme.CyanHi * (0.95f * a)
                    : isHover ? SHPCTheme.Border * (0.9f * a)
                    : SHPCTheme.Border * (0.55f * a);
                SHPCRenderer.DrawRectStroke(sb, px, r, 1.1f, border);

                if (isHover || isActive) {
                    SHPCRenderer.DrawCornerBrackets(sb, px, r, 4f, 1.1f,
                        (isActive ? SHPCTheme.CyanHi : SHPCTheme.BorderHi) * a);
                }

                //类别字母 glyph
                string glyph = GetGlyph(cat);
                Vector2 glyphSz = font.MeasureString(glyph) * 0.8f;
                Utils.DrawBorderString(sb, glyph,
                    new Vector2(r.X + 12f, r.Y + (r.Height - glyphSz.Y) * 0.5f),
                    band * a, 0.8f);

                //类别名
                string name = Language.GetTextValue("Mods.CalamityOverhaul.Legend.SHPCSlotName." + cat);
                Utils.DrawBorderString(sb, name,
                    new Vector2(r.X + 34f, r.Y + 6f),
                    (isActive ? SHPCTheme.Text : SHPCTheme.TextDim) * a, 0.55f);

                //碎片数 + 图鉴进度
                int shards = sp?.MoldShards != null && i < sp.MoldShards.Length ? sp.MoldShards[i] : 0;
                int discovered = sp != null && sp.DiscoveredModules != null
                    ? MoldRecipeSystem.EnumerateCategoryAll(cat).Count(t => sp.DiscoveredModules.Contains(t))
                    : 0;
                int total = MoldRecipeSystem.EnumerateCategoryAll(cat).Count();

                string status = $"{shards} {MoldProcessingUI.ShardSuffix.Value}  ·  {discovered}/{total}";
                Color statusCol = (isActive ? SHPCTheme.Cyan : SHPCTheme.TextDim) * a;
                Utils.DrawBorderString(sb, status,
                    new Vector2(r.X + 34f, r.Y + 24f), statusCol, 0.48f);
            }
        }

        private static string GetGlyph(SHPCSlotCategory cat) => cat switch {
            SHPCSlotCategory.Barrel => "B",
            SHPCSlotCategory.Optic => "O",
            SHPCSlotCategory.Power => "P",
            SHPCSlotCategory.Stock => "S",
            SHPCSlotCategory.Grip => "G",
            SHPCSlotCategory.Frame => "F",
            _ => "?",
        };
    }
}
