using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>
    /// 图鉴 Tab：当前选中类别下的所有模块网格，已发现彩色 + 钉选交互；未发现剪影 + ???
    /// 悬停已发现模块时弹出 Tooltip 卡片（仿 <see cref="SHPCModuleSelectPanel"/>.DrawCustomTooltip）
    /// </summary>
    internal static class MoldCodexPanel
    {
        private const int Columns = 5;
        private const float CellSize = 60f;
        private const float CellGap = 6f;
        private const int HeaderH = 28;

        private static int scrollOffset;
        //当前类别快照（每帧刷新）
        private static readonly List<int> currentTypes = new();
        //当前帧悬停的格子索引（-1 = 无）
        private static int hoverIndex = -1;

        public static void ScrollReset() => scrollOffset = 0;

        public static void HandleScroll() {
            int delta = PlayerInput.ScrollWheelDeltaForUI;
            if (delta == 0) {
                return;
            }
            int rows = (currentTypes.Count + Columns - 1) / Columns;
            int maxVisibleRows = VisibleRowsCount();
            int maxScroll = Math.Max(0, rows - maxVisibleRows);
            if (maxScroll == 0) {
                return;
            }
            int old = scrollOffset;
            scrollOffset = Math.Clamp(scrollOffset - Math.Sign(delta), 0, maxScroll);
            if (scrollOffset != old) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = scrollOffset > old ? 0.1f : -0.1f });
            }
        }

        public static void UpdateHover(in MoldLayout layout, Vector2 mouse, MoldProcessingUI ui) {
            hoverIndex = -1;
            RefreshTypesForCategory(ui.SelectedCategory);

            Rectangle gridArea = GetGridArea(layout);
            if (!gridArea.Contains((int)mouse.X, (int)mouse.Y)) {
                return;
            }
            int relX = (int)mouse.X - gridArea.X;
            int relY = (int)mouse.Y - gridArea.Y;
            int col = (int)(relX / (CellSize + CellGap));
            int rowVis = (int)(relY / (CellSize + CellGap));
            if (col < 0 || col >= Columns || rowVis < 0 || rowVis >= VisibleRowsCount()) {
                return;
            }
            //子区域内是否真的命中（去掉间隔）
            float localX = relX - col * (CellSize + CellGap);
            float localY = relY - rowVis * (CellSize + CellGap);
            if (localX > CellSize || localY > CellSize) {
                return;
            }
            int absoluteIdx = (rowVis + scrollOffset) * Columns + col;
            if (absoluteIdx >= 0 && absoluteIdx < currentTypes.Count) {
                hoverIndex = absoluteIdx;
            }
        }

        public static void HandleClick(MoldProcessingUI ui, Player owner) {
            if (hoverIndex < 0 || hoverIndex >= currentTypes.Count) {
                return;
            }
            int type = currentTypes[hoverIndex];
            SHPCPlayer sp = SHPCPlayer.Get(owner);
            if (sp == null) {
                return;
            }
            //未发现不可点击
            if (sp.DiscoveredModules == null || !sp.DiscoveredModules.Contains(type)) {
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }
            int idx = (int)ui.SelectedCategory;
            int currentPin = sp.PinnedReforgeTarget != null && idx < sp.PinnedReforgeTarget.Length
                ? sp.PinnedReforgeTarget[idx] : -1;
            //点击当前已钉选 -> 取消，否则切换为该 type
            int newTarget = currentPin == type ? -1 : type;
            if (sp.TryPinReforge(ui.SelectedCategory, newTarget)) {
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        public static void Draw(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            in MoldLayout layout, MoldProcessingUI ui, float a) {
            //内容区背景
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(layout.Content.X + 2, layout.Content.Y + 2,
                    layout.Content.Width, layout.Content.Height),
                new Color(0, 0, 0) * (0.35f * a));
            SHPCRenderer.DrawFilledRect(sb, px, layout.Content,
                new Color(4, 14, 22) * (0.85f * a));
            SHPCRenderer.DrawRectStroke(sb, px, layout.Content, 1.1f, SHPCTheme.Border * (0.7f * a));

            //顶部标题与提示
            RefreshTypesForCategory(ui.SelectedCategory);
            Player p = Main.LocalPlayer;
            SHPCPlayer sp = p != null ? SHPCPlayer.Get(p) : null;
            int discCount = sp != null && sp.DiscoveredModules != null
                ? currentTypes.Count(t => sp.DiscoveredModules.Contains(t)) : 0;
            string progress = string.Format(MoldProcessingUI.ProgressFormat.Value, discCount, currentTypes.Count);
            string header = $"{MoldProcessingUI.Discovered.Value}  ·  {progress}";
            float headerScale = MoldFont.ColumnTitleBase * MoldFont.FontScale;

            //先按比例分配左右空间：标题最多占 60%，剩余给右侧提示
            float headerMaxW = layout.Content.Width * 0.6f - 8f;
            string headerDraw = MoldFont.TruncateForWidth(font, header, headerMaxW, headerScale);
            Utils.DrawBorderString(sb, headerDraw,
                new Vector2(layout.Content.X + 8f, layout.Content.Y + 6f), SHPCTheme.Text * a, headerScale);

            float hintScale = MoldFont.HintBase * MoldFont.FontScale;
            string hint = MoldProcessingUI.CodexHint.Value;
            //提示文字可用宽度：从内容右边缘往左到 header 实际占用宽度之外
            float headerActualW = font.MeasureString(headerDraw).X * headerScale;
            float hintMaxW = layout.Content.Width - 16f - headerActualW - 12f;
            if (hintMaxW < 60f) {
                hintMaxW = 60f;
            }
            string hintDraw = MoldFont.TruncateForWidth(font, hint, hintMaxW, hintScale);
            Vector2 hintSz = font.MeasureString(hintDraw) * hintScale;
            Utils.DrawBorderString(sb, hintDraw,
                new Vector2(layout.Content.Right - 8f - hintSz.X, layout.Content.Y + 8f),
                SHPCTheme.TextDim * (0.9f * a), hintScale);

            //网格区
            Rectangle gridArea = GetGridArea(layout);
            SHPCRenderer.DrawFilledRect(sb, px, gridArea, new Color(2, 8, 14) * (0.7f * a));
            SHPCRenderer.DrawRectStroke(sb, px, gridArea, 1f, SHPCTheme.Border * (0.45f * a));

            int currentPin = sp?.PinnedReforgeTarget != null && (int)ui.SelectedCategory < sp.PinnedReforgeTarget.Length
                ? sp.PinnedReforgeTarget[(int)ui.SelectedCategory] : -1;

            int visibleRows = VisibleRowsCount();
            int startRow = scrollOffset;
            int total = currentTypes.Count;

            for (int rv = 0; rv < visibleRows; rv++) {
                for (int c = 0; c < Columns; c++) {
                    int absIdx = (rv + startRow) * Columns + c;
                    if (absIdx >= total) {
                        break;
                    }
                    Rectangle cell = new(
                        (int)(gridArea.X + c * (CellSize + CellGap)),
                        (int)(gridArea.Y + rv * (CellSize + CellGap)),
                        (int)CellSize, (int)CellSize);
                    int type = currentTypes[absIdx];
                    bool discovered = sp != null && sp.DiscoveredModules != null
                        && sp.DiscoveredModules.Contains(type);
                    bool isHover = hoverIndex == absIdx;
                    bool isPinned = currentPin > 0 && currentPin == type;

                    DrawCell(sb, px, font, cell, type, discovered, isHover, isPinned, a);
                }
            }

            //滚动条
            int rows = (total + Columns - 1) / Columns;
            if (rows > visibleRows) {
                DrawGridScrollbar(sb, px, gridArea, rows, visibleRows, scrollOffset, a);
            }

            //悬停 tooltip
            if (hoverIndex >= 0 && hoverIndex < total) {
                int type = currentTypes[hoverIndex];
                bool discovered = sp != null && sp.DiscoveredModules != null
                    && sp.DiscoveredModules.Contains(type);
                if (discovered && ContentSamples.ItemsByType.TryGetValue(type, out Item sample)) {
                    DrawCustomTooltip(sb, px, font, sample, a);
                }
            }
        }

        private static Rectangle GetGridArea(in MoldLayout layout) {
            int hintBarH = 6;
            return new Rectangle(
                layout.Content.X + 6,
                layout.Content.Y + HeaderH + hintBarH,
                layout.Content.Width - 12,
                layout.Content.Height - HeaderH - hintBarH - 6);
        }

        private static int VisibleRowsCount() {
            //内容区约 332 高，扣掉 header(28)+pad(12)+下方 pad(6) -> 约 286；行高 66 -> 4 行
            //取保守值 4 以避免 layout 漂移
            return 4;
        }

        private static void RefreshTypesForCategory(SHPCSlotCategory cat) {
            currentTypes.Clear();
            //完整池（含 lab=false 的隐藏件），稳定排序：先按是否发现，再按 type
            Player p = Main.LocalPlayer;
            SHPCPlayer sp = p != null ? SHPCPlayer.Get(p) : null;
            HashSet<int> disc = sp?.DiscoveredModules ?? new HashSet<int>();
            currentTypes.AddRange(MoldRecipeSystem.EnumerateCategoryAll(cat)
                .OrderByDescending(t => disc.Contains(t))
                .ThenBy(t => t));
        }

        private static void DrawCell(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle cell, int type, bool discovered, bool isHover, bool isPinned, float a) {
            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(cell.X + 2, cell.Y + 2, cell.Width, cell.Height),
                new Color(0, 0, 0) * (0.4f * a));

            Color bg = isPinned ? new Color(40, 30, 12) * (0.95f * a)
                : isHover ? new Color(12, 50, 70) * (0.9f * a)
                : new Color(6, 20, 30) * (0.85f * a);
            SHPCRenderer.DrawFilledRect(sb, px, cell, bg);

            //顶部色带：钉选用 accent，已发现用 module tint，未发现用 dim border
            Color topBar;
            if (isPinned) {
                topBar = SHPCTheme.Accent;
            }
            else if (discovered && ContentSamples.ItemsByType.TryGetValue(type, out Item discSample)
                && discSample.ModItem is SHPCModuleItem mod) {
                topBar = mod.TintColor;
            }
            else {
                topBar = SHPCTheme.Border;
            }
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(cell.X, cell.Y, cell.Width, 2),
                topBar * (0.85f * a));

            Color border = isPinned ? SHPCTheme.Accent * (0.95f * a)
                : isHover ? SHPCTheme.CyanHi * (0.9f * a)
                : SHPCTheme.Border * (0.55f * a);
            SHPCRenderer.DrawRectStroke(sb, px, cell, 1.1f, border);

            if (isPinned) {
                SHPCRenderer.DrawCornerBrackets(sb, px, cell, 5f, 1.3f, SHPCTheme.Accent * a);
            }
            else if (isHover) {
                SHPCRenderer.DrawCornerBrackets(sb, px, cell, 4f, 1.1f, SHPCTheme.CyanHi * a);
            }

            //图标
            Main.instance.LoadItem(type);
            Texture2D iconTex = TextureAssets.Item[type]?.Value;
            if (iconTex != null) {
                Rectangle frame = Main.itemAnimations[type] != null
                    ? Main.itemAnimations[type].GetFrame(iconTex)
                    : iconTex.Bounds;
                float maxIcon = cell.Width - 14f;
                float iconScale = MathF.Min(maxIcon / frame.Width, maxIcon / frame.Height);
                if (iconScale > 1.4f) iconScale = 1.4f;
                Vector2 center = new(cell.Center.X, cell.Center.Y + 2f);

                if (discovered && ContentSamples.ItemsByType.TryGetValue(type, out Item s2)
                    && s2.ModItem is SHPCModuleItem mod2
                    && SHPCModuleRender.Begin(sb, mod2.TintColor,
                        new Vector2(iconTex.Width, iconTex.Height), Main.UIScaleMatrix, mod2.TintIntensity)) {
                    sb.Draw(iconTex, center, frame, Color.White * a, 0f,
                        new Vector2(frame.Width * 0.5f, frame.Height * 0.5f), iconScale, SpriteEffects.None, 0f);
                    SHPCModuleRender.End(sb);
                }
                else {
                    //未发现：纯黑剪影 + 中央 ? 字符叠加
                    sb.Draw(iconTex, center, frame, new Color(0, 6, 10) * (0.9f * a), 0f,
                        new Vector2(frame.Width * 0.5f, frame.Height * 0.5f), iconScale, SpriteEffects.None, 0f);
                    float qScale = MoldFont.CodexQmarkBase * MoldFont.FontScale;
                    Vector2 qSz = font.MeasureString("?") * qScale;
                    Utils.DrawBorderString(sb, "?",
                        new Vector2(center.X - qSz.X * 0.5f, center.Y - qSz.Y * 0.5f),
                        SHPCTheme.Border * (0.95f * a), qScale);
                }
            }

            //PINNED 标签（必要时截断防溢出格子）
            if (isPinned) {
                string tag = MoldProcessingUI.PinnedTag.Value;
                float tagScale = MoldFont.PinnedTagBase * MoldFont.FontScale;
                string tagDraw = MoldFont.TruncateForWidth(font, tag, cell.Width - 4f, tagScale);
                Vector2 ts = font.MeasureString(tagDraw) * tagScale;
                Utils.DrawBorderString(sb, tagDraw,
                    new Vector2(cell.X + (cell.Width - ts.X) * 0.5f, cell.Bottom - ts.Y - 2f),
                    SHPCTheme.Accent * a, tagScale);
            }
        }

        private static void DrawGridScrollbar(SpriteBatch sb, Texture2D px,
            Rectangle area, int totalRows, int visibleRows, int offset, float a) {
            const float sbW = 4f;
            const float sbGap = 2f;
            float trackH = area.Height - 4f;
            float thumbRatio = MathF.Min(1f, (float)visibleRows / totalRows);
            float thumbH = MathF.Max(14f, trackH * thumbRatio);
            float maxScroll = totalRows - visibleRows;
            float thumbY = maxScroll > 0 ? offset / maxScroll * (trackH - thumbH) : 0f;
            Rectangle track = new(area.Right - (int)(sbW + sbGap), area.Y + 2, (int)sbW, (int)trackH);
            Rectangle thumb = new(track.X, track.Y + (int)thumbY, track.Width, (int)thumbH);
            SHPCRenderer.DrawFilledRect(sb, px, track, SHPCTheme.Border * (0.25f * a));
            SHPCRenderer.DrawFilledRect(sb, px, thumb, SHPCTheme.Cyan * (0.7f * a));
        }

        private static void DrawCustomTooltip(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Item item, float a) {
            //仿 SHPCModuleSelectPanel.DrawCustomTooltip
            List<string> lines = new();
            List<Color> colors = new();

            lines.Add(item.Name);
            colors.Add(SHPCTheme.Text);

            if (item.ToolTip != null) {
                int n = item.ToolTip.Lines;
                for (int i = 0; i < n; i++) {
                    string ln = item.ToolTip.GetLine(i);
                    if (!string.IsNullOrWhiteSpace(ln)) {
                        lines.Add(ln);
                        colors.Add(SHPCTheme.TextDim);
                    }
                }
            }
            if (item.ModItem is SHPCModuleItem mod) {
                foreach (var (ln, isNeg) in mod.GetStatLines()) {
                    if (string.IsNullOrEmpty(ln)) continue;
                    lines.Add(ln);
                    colors.Add(isNeg ? new Color(255, 120, 110) : new Color(120, 255, 170));
                }
            }

            float scale = MoldFont.TooltipBase * MoldFont.FontScale;
            float lineH = font.LineSpacing * scale;
            //单行最大像素宽度，超过则按字符截断（防超长 stat 描述把卡片撑到屏幕外）
            float lineMaxW = MathF.Min(420f, Main.screenWidth - 32f);
            for (int i = 0; i < lines.Count; i++) {
                lines[i] = MoldFont.TruncateForWidth(font, lines[i], lineMaxW, scale);
            }
            float maxW = 0f;
            for (int i = 0; i < lines.Count; i++) {
                float w = font.MeasureString(lines[i]).X * scale;
                if (w > maxW) maxW = w;
            }
            const float padX = 8f;
            const float padY = 6f;
            Vector2 mouse = Main.MouseScreen;
            Rectangle box = new((int)(mouse.X + 16f), (int)(mouse.Y + 16f),
                (int)(maxW + padX * 2), (int)(lineH * lines.Count + padY * 2));
            if (box.Right > Main.screenWidth) box.X = Main.screenWidth - box.Width - 4;
            if (box.Bottom > Main.screenHeight) box.Y = Main.screenHeight - box.Height - 4;
            if (box.X < 4) box.X = 4;
            if (box.Y < 4) box.Y = 4;

            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(box.X + 3, box.Y + 4, box.Width, box.Height),
                new Color(0, 0, 0) * (0.6f * a));
            SHPCRenderer.DrawFilledRect(sb, px, box, new Color(4, 14, 22) * (0.96f * a));

            Color topBar = item.ModItem is SHPCModuleItem m ? m.TintColor : SHPCTheme.Cyan;
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(box.X, box.Y, box.Width, 3), topBar * (0.85f * a));
            SHPCRenderer.DrawRectStroke(sb, px, box, 1.2f, SHPCTheme.Border * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, box, 6f, 1.2f, SHPCTheme.BorderHi * (0.9f * a));

            float y = box.Y + padY;
            for (int i = 0; i < lines.Count; i++) {
                Utils.DrawBorderString(sb, lines[i],
                    new Vector2(box.X + padX, y), colors[i] * a, scale);
                y += lineH;
            }
        }
    }
}
