using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.MoldProcessingTables.UI
{
    /// <summary>
    /// 工作台 Tab：
    ///   左栏 - 当前类别的可分解模块列表（来自玩家背包，复用 SHPCModuleSelectPanel 行布局）
    ///   右栏 - 重铸目标预览 + REFORGE / CLEAR PIN 按钮 + 成本与碎片显示
    /// </summary>
    internal static class MoldProcessingPanel
    {
        private const float RowH = 34f;
        private const float IconSize = 26f;
        private const float RowPadX = 8f;

        private static readonly List<Item> candidates = new();
        private static int scrollOffset;

        public enum HitKind { None, Row, Reforge, ClearPin }
        private static HitKind hitKind;
        //当前命中行（绝对索引）
        private static int hoveredRow = -1;

        public static IReadOnlyList<Item> CurrentCandidates => candidates;

        public static void ScrollReset() => scrollOffset = 0;

        public static void RefreshCandidates(Player player, SHPCSlotCategory category) {
            candidates.Clear();
            if (player == null || !player.active) {
                return;
            }
            for (int i = 0; i < Main.InventorySlotsTotal && i < player.inventory.Length; i++) {
                Item it = player.inventory[i];
                if (it == null || it.IsAir) {
                    continue;
                }
                if (it.ModItem is SHPCModuleItem mod && mod.SlotCategory == category) {
                    candidates.Add(it);
                }
            }
        }

        public static void HandleScroll() {
            int delta = PlayerInput.ScrollWheelDeltaForUI;
            if (delta == 0) {
                return;
            }
            int maxScroll = Math.Max(0, candidates.Count - MaxVisibleRows());
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
            hitKind = HitKind.None;
            hoveredRow = -1;

            (Rectangle leftCol, Rectangle rightCol) = SplitContent(layout);
            Rectangle listArea = GetListArea(leftCol);

            //行命中
            if (listArea.Contains((int)mouse.X, (int)mouse.Y) && candidates.Count > 0) {
                int rel = (int)mouse.Y - listArea.Y;
                int visualIdx = rel / (int)RowH;
                int candidateIdx = visualIdx + scrollOffset;
                if (visualIdx >= 0 && visualIdx < MaxVisibleRows() && candidateIdx < candidates.Count) {
                    hitKind = HitKind.Row;
                    hoveredRow = candidateIdx;
                    return;
                }
            }

            (Rectangle reforge, Rectangle clearPin) = GetReforgeButtons(rightCol);
            if (reforge.Contains((int)mouse.X, (int)mouse.Y)) {
                hitKind = HitKind.Reforge;
                return;
            }
            //仅当存在钉选目标时 ClearPin 才有效
            SHPCPlayer sp = SHPCPlayer.Get(Main.LocalPlayer);
            int idx = (int)ui.SelectedCategory;
            bool pinned = sp != null && sp.PinnedReforgeTarget != null && sp.PinnedReforgeTarget[idx] > 0;
            if (pinned && clearPin.Contains((int)mouse.X, (int)mouse.Y)) {
                hitKind = HitKind.ClearPin;
                return;
            }
        }

        public static void HandleClick(MoldProcessingUI ui, Player owner) {
            if (hitKind == HitKind.Row) {
                if (hoveredRow >= 0 && hoveredRow < candidates.Count) {
                    Item picked = candidates[hoveredRow];
                    int slotIdx = -1;
                    for (int i = 0; i < owner.inventory.Length; i++) {
                        if (owner.inventory[i] == picked) {
                            slotIdx = i;
                            break;
                        }
                    }
                    if (slotIdx >= 0 && MoldRecipeSystem.TryDecompose(owner, slotIdx, out int _)) {
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f });
                    }
                }
                return;
            }
            if (hitKind == HitKind.Reforge) {
                SHPCPlayer sp = SHPCPlayer.Get(owner);
                int idx = (int)ui.SelectedCategory;
                bool pinned = sp != null && sp.PinnedReforgeTarget != null && sp.PinnedReforgeTarget[idx] > 0;
                bool ok = pinned
                    ? MoldRecipeSystem.TryReforgePinned(owner, ui.SelectedCategory, out _)
                    : MoldRecipeSystem.TryReforgeRandom(owner, ui.SelectedCategory, out _);
                if (ok) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.1f });
                }
                else {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
                return;
            }
            if (hitKind == HitKind.ClearPin) {
                SHPCPlayer sp = SHPCPlayer.Get(owner);
                if (sp != null && sp.TryPinReforge(ui.SelectedCategory, -1)) {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        public static void Draw(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            in MoldLayout layout, MoldProcessingUI ui, float a) {
            (Rectangle leftCol, Rectangle rightCol) = SplitContent(layout);
            DrawDecomposeColumn(sb, px, font, leftCol, ui, a);
            DrawReforgeColumn(sb, px, font, rightCol, ui, a);
        }

        private static (Rectangle, Rectangle) SplitContent(in MoldLayout layout) {
            int halfW = (layout.Content.Width - 6) / 2;
            Rectangle left = new(layout.Content.X, layout.Content.Y, halfW, layout.Content.Height);
            Rectangle right = new(layout.Content.X + halfW + 6, layout.Content.Y,
                layout.Content.Width - halfW - 6, layout.Content.Height);
            return (left, right);
        }

        private static Rectangle GetListArea(Rectangle leftCol) {
            int headerH = 28;
            int hintH = 18;
            return new Rectangle(
                leftCol.X + 6,
                leftCol.Y + headerH,
                leftCol.Width - 12,
                leftCol.Height - headerH - hintH - 8);
        }

        private static int MaxVisibleRows() {
            //缓存 layout 不便，这里用一个固定上限：内容区高度约 360，列表区约 340，34 行 -> 10 行
            return 10;
        }

        private static (Rectangle reforge, Rectangle clearPin) GetReforgeButtons(Rectangle rightCol) {
            int btnH = 32;
            int gap = 6;
            Rectangle reforge = new(
                rightCol.X + 12,
                rightCol.Bottom - 12 - btnH,
                rightCol.Width - 24,
                btnH);
            Rectangle clearPin = new(
                rightCol.X + 12,
                reforge.Y - gap - btnH,
                rightCol.Width - 24,
                btnH);
            return (reforge, clearPin);
        }

        private static void DrawDecomposeColumn(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle col, MoldProcessingUI ui, float a) {
            //列背景
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(col.X + 2, col.Y + 2, col.Width, col.Height),
                new Color(0, 0, 0) * (0.35f * a));
            SHPCRenderer.DrawFilledRect(sb, px, col, new Color(4, 14, 22) * (0.85f * a));
            SHPCRenderer.DrawRectStroke(sb, px, col, 1.1f, SHPCTheme.Border * (0.7f * a));

            //标题
            Utils.DrawBorderString(sb, MoldProcessingUI.Decompose.Value,
                new Vector2(col.X + 8f, col.Y + 6f), SHPCTheme.Text * a, 0.65f);

            Rectangle listArea = GetListArea(col);
            SHPCRenderer.DrawFilledRect(sb, px, listArea, new Color(2, 8, 14) * (0.7f * a));
            SHPCRenderer.DrawRectStroke(sb, px, listArea, 1f, SHPCTheme.Border * (0.45f * a));

            int count = candidates.Count;
            int maxVisible = MaxVisibleRows();

            if (count == 0) {
                string empty = MoldProcessingUI.EmptyCandidates.Value;
                Vector2 sz = font.MeasureString(empty) * 0.55f;
                Utils.DrawBorderString(sb, empty,
                    new Vector2(listArea.X + (listArea.Width - sz.X) * 0.5f,
                        listArea.Y + (listArea.Height - sz.Y) * 0.5f),
                    SHPCTheme.TextDim * a, 0.55f);
            }
            else {
                bool needScrollbar = count > maxVisible;
                int sbReserve = needScrollbar ? 7 : 0;

                int start = scrollOffset;
                int end = Math.Min(count, start + maxVisible);
                for (int i = start; i < end; i++) {
                    int visRow = i - start;
                    Rectangle row = new(listArea.X + 2,
                        listArea.Y + 2 + visRow * (int)RowH,
                        listArea.Width - 4 - sbReserve, (int)RowH - 2);

                    bool isHover = hitKind == HitKind.Row && hoveredRow == i;

                    Color rowBg = isHover ? new Color(12, 50, 70) * (0.85f * a)
                        : new Color(6, 20, 30) * (0.7f * a);
                    SHPCRenderer.DrawFilledRect(sb, px, row, rowBg);
                    Color rowBorder = isHover ? SHPCTheme.CyanHi * (0.9f * a)
                        : SHPCTheme.Border * (0.55f * a);
                    SHPCRenderer.DrawRectStroke(sb, px, row, 1f, rowBorder);

                    //左色条
                    Color band = candidates[i].ModItem is SHPCModuleItem mod
                        ? mod.TintColor : SHPCTheme.Cyan;
                    SHPCRenderer.DrawFilledRect(sb, px,
                        new Rectangle(row.X, row.Y, 3, row.Height),
                        band * (isHover ? 1f * a : 0.7f * a));

                    DrawItemIcon(sb, candidates[i],
                        new Vector2(row.X + RowPadX + IconSize * 0.5f, row.Y + row.Height * 0.5f),
                        IconSize, a);

                    Vector2 textPos = new(row.X + RowPadX + IconSize + 6f,
                        row.Y + (row.Height - font.LineSpacing * 0.5f) * 0.5f);
                    Color nameCol = (isHover ? SHPCTheme.Text : SHPCTheme.TextDim) * a;
                    Utils.DrawBorderString(sb, candidates[i].Name, textPos, nameCol, 0.6f);

                    //右侧 +N 提示
                    string gainTag = $"+{MoldRecipeSystem.DecomposeGain}";
                    Vector2 ts = font.MeasureString(gainTag) * 0.55f;
                    Utils.DrawBorderString(sb, gainTag,
                        new Vector2(row.Right - 6f - ts.X, textPos.Y),
                        new Color(120, 255, 170) * a, 0.55f);
                }

                if (needScrollbar) {
                    DrawListScrollbar(sb, px, listArea, count, maxVisible, scrollOffset, a);
                }
            }

            //底部小提示
            Utils.DrawBorderString(sb, MoldProcessingUI.DecomposeHint.Value,
                new Vector2(col.X + 8f, col.Bottom - 16f),
                SHPCTheme.TextDim * (0.85f * a), 0.45f);
        }

        private static void DrawReforgeColumn(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle col, MoldProcessingUI ui, float a) {
            //列背景
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(col.X + 2, col.Y + 2, col.Width, col.Height),
                new Color(0, 0, 0) * (0.35f * a));
            SHPCRenderer.DrawFilledRect(sb, px, col, new Color(4, 14, 22) * (0.85f * a));
            SHPCRenderer.DrawRectStroke(sb, px, col, 1.1f, SHPCTheme.Border * (0.7f * a));

            //标题
            Utils.DrawBorderString(sb, MoldProcessingUI.Reforge.Value,
                new Vector2(col.X + 8f, col.Y + 6f), SHPCTheme.Text * a, 0.65f);

            //预览框
            int previewSize = 100;
            Rectangle preview = new(
                col.X + (col.Width - previewSize) / 2,
                col.Y + 36,
                previewSize, previewSize);
            DrawReforgePreview(sb, px, font, preview, ui, a);

            //模式描述（钉选/随机）
            Player p = Main.LocalPlayer;
            SHPCPlayer sp = p != null ? SHPCPlayer.Get(p) : null;
            int idx = (int)ui.SelectedCategory;
            int target = sp?.PinnedReforgeTarget != null && idx < sp.PinnedReforgeTarget.Length
                ? sp.PinnedReforgeTarget[idx] : -1;
            bool pinned = target > 0;

            string modeText;
            string targetName;
            if (pinned && ContentSamples.ItemsByType.TryGetValue(target, out Item targetSample)) {
                modeText = MoldProcessingUI.PinnedMode.Value;
                targetName = targetSample.Name;
            }
            else {
                modeText = MoldProcessingUI.RandomMode.Value;
                targetName = MoldProcessingUI.UnknownName.Value;
            }

            Vector2 modeSz = font.MeasureString(modeText) * 0.5f;
            Utils.DrawBorderString(sb, modeText,
                new Vector2(col.X + (col.Width - modeSz.X) * 0.5f, preview.Bottom + 8f),
                (pinned ? SHPCTheme.Accent : SHPCTheme.Cyan) * a, 0.5f);

            Vector2 nameSz = font.MeasureString(targetName) * 0.6f;
            Utils.DrawBorderString(sb, targetName,
                new Vector2(col.X + (col.Width - nameSz.X) * 0.5f, preview.Bottom + 26f),
                SHPCTheme.Text * a, 0.6f);

            //成本 / 持有
            int cost = pinned ? MoldRecipeSystem.PinnedCost : MoldRecipeSystem.RandomCost;
            int have = sp?.MoldShards != null && idx < sp.MoldShards.Length ? sp.MoldShards[idx] : 0;
            bool canAfford = have >= cost;

            string costLine = string.Format(MoldProcessingUI.CostLine.Value, cost);
            string haveLine = string.Format(MoldProcessingUI.HaveLine.Value, have);
            Vector2 costSz = font.MeasureString(costLine) * 0.52f;
            Vector2 haveSz = font.MeasureString(haveLine) * 0.52f;

            float costY = preview.Bottom + 50f;
            Utils.DrawBorderString(sb, costLine,
                new Vector2(col.X + 18f, costY),
                (canAfford ? new Color(120, 255, 170) : new Color(255, 120, 110)) * a, 0.52f);
            Utils.DrawBorderString(sb, haveLine,
                new Vector2(col.Right - 18f - haveSz.X, costY),
                SHPCTheme.Text * a, 0.52f);

            //按钮
            (Rectangle reforge, Rectangle clearPin) = GetReforgeButtons(col);
            DrawBigButton(sb, px, font, reforge,
                MoldProcessingUI.Reforge.Value,
                hitKind == HitKind.Reforge,
                canAfford,
                pinned ? SHPCTheme.Accent : SHPCTheme.Cyan,
                a);
            if (pinned) {
                DrawBigButton(sb, px, font, clearPin,
                    MoldProcessingUI.ClearPin.Value,
                    hitKind == HitKind.ClearPin,
                    true,
                    SHPCTheme.Border,
                    a, smaller: true);
            }
        }

        private static void DrawReforgePreview(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle r, MoldProcessingUI ui, float a) {
            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X + 2, r.Y + 3, r.Width, r.Height),
                new Color(0, 0, 0) * (0.55f * a));

            //背景
            SHPCRenderer.DrawFilledRect(sb, px, r, new Color(2, 10, 16) * (0.95f * a));

            //旋转脉冲环背景
            float time = (float)Main.GameUpdateCount / 60f;
            Vector2 center = new(r.Center.X, r.Center.Y);
            for (int g = 3; g >= 1; g--) {
                float pulse = 0.6f + MathF.Sin(time * 1.8f + g * 0.6f) * 0.4f;
                SHPCRenderer.DrawRing(sb, px, center, 30f + g * 8f, 1.5f,
                    SHPCTheme.Cyan * (0.12f * pulse * a));
            }

            Player p = Main.LocalPlayer;
            SHPCPlayer sp = p != null ? SHPCPlayer.Get(p) : null;
            int idx = (int)ui.SelectedCategory;
            int target = sp?.PinnedReforgeTarget != null && idx < sp.PinnedReforgeTarget.Length
                ? sp.PinnedReforgeTarget[idx] : -1;

            if (target > 0 && ContentSamples.ItemsByType.TryGetValue(target, out Item targetSample)) {
                //绘制钉选模块图标（应用赛博朋克滤镜）
                Main.instance.LoadItem(target);
                Texture2D iconTex = TextureAssets.Item[target]?.Value;
                if (iconTex != null) {
                    Rectangle frame = Main.itemAnimations[target] != null
                        ? Main.itemAnimations[target].GetFrame(iconTex)
                        : iconTex.Bounds;
                    float maxIcon = MathF.Min(r.Width, r.Height) - 24f;
                    float iconScale = MathF.Min(maxIcon / frame.Width, maxIcon / frame.Height);
                    if (iconScale > 2.4f) iconScale = 2.4f;
                    if (targetSample.ModItem is SHPCModuleItem mod
                        && SHPCModuleRender.Begin(sb, mod.TintColor,
                            new Vector2(iconTex.Width, iconTex.Height), Main.UIScaleMatrix, mod.TintIntensity)) {
                        sb.Draw(iconTex, center, frame, Color.White * a, 0f,
                            new Vector2(frame.Width * 0.5f, frame.Height * 0.5f),
                            iconScale, SpriteEffects.None, 0f);
                        SHPCModuleRender.End(sb);
                    }
                    else {
                        sb.Draw(iconTex, center, frame, Color.White * a, 0f,
                            new Vector2(frame.Width * 0.5f, frame.Height * 0.5f),
                            iconScale, SpriteEffects.None, 0f);
                    }
                }
            }
            else {
                //随机模式：绘制旋转加载点
                int dotCount = 8;
                float radius = 24f;
                for (int i = 0; i < dotCount; i++) {
                    float ang = i * MathF.Tau / dotCount + time * 1.5f;
                    Vector2 dotPos = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                    float bright = 0.4f + 0.6f * (i / (float)dotCount);
                    SHPCRenderer.DrawDisc(sb, px, dotPos, 2.2f, 2f, SHPCTheme.Cyan * (bright * a));
                }
                //中心闪烁问号
                Vector2 qSz = font.MeasureString("?") * 1.2f;
                float qPulse = 0.85f + MathF.Sin(time * 2.6f) * 0.15f;
                Utils.DrawBorderString(sb, "?",
                    new Vector2(center.X - qSz.X * 0.5f, center.Y - qSz.Y * 0.5f),
                    SHPCTheme.CyanHi * (qPulse * a), 1.2f);
            }

            //外框
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.2f, SHPCTheme.Border * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, r, 6f, 1.3f,
                (target > 0 ? SHPCTheme.Accent : SHPCTheme.CyanHi) * a);
        }

        private static void DrawBigButton(SpriteBatch sb, Texture2D px, DynamicSpriteFont font,
            Rectangle r, string label, bool isHover, bool enabled, Color accent, float a, bool smaller = false) {
            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height),
                new Color(0, 0, 0) * (0.45f * a));

            Color bg = !enabled ? new Color(20, 20, 20) * (0.65f * a)
                : isHover ? new Color(18, 60, 80) * (0.95f * a)
                : new Color(8, 26, 36) * (0.85f * a);
            SHPCRenderer.DrawFilledRect(sb, px, r, bg);

            Color border = !enabled ? SHPCTheme.Disabled * (0.7f * a)
                : isHover ? accent * (0.95f * a)
                : SHPCTheme.Border * (0.7f * a);
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.2f, border);

            if (isHover && enabled) {
                SHPCRenderer.DrawCornerBrackets(sb, px, r, 5f, 1.2f, accent * a);
            }

            float scale = smaller ? 0.6f : 0.78f;
            Vector2 sz = font.MeasureString(label) * scale;
            Color textCol = !enabled ? SHPCTheme.Disabled * a
                : isHover ? SHPCTheme.Text * a : SHPCTheme.TextDim * a;
            Utils.DrawBorderString(sb, label,
                new Vector2(r.X + (r.Width - sz.X) * 0.5f, r.Y + (r.Height - sz.Y) * 0.5f),
                textCol, scale);
        }

        private static void DrawListScrollbar(SpriteBatch sb, Texture2D px,
            Rectangle listArea, int totalCount, int maxVisible, int offset, float a) {
            const float sbW = 4f;
            const float sbGap = 2f;
            float trackH = listArea.Height - 4f;
            float thumbRatio = MathF.Min(1f, (float)maxVisible / totalCount);
            float thumbH = MathF.Max(14f, trackH * thumbRatio);
            float maxScroll = totalCount - maxVisible;
            float thumbY = maxScroll > 0 ? offset / maxScroll * (trackH - thumbH) : 0f;
            Rectangle track = new(listArea.Right - (int)(sbW + sbGap), listArea.Y + 2, (int)sbW, (int)trackH);
            Rectangle thumb = new(track.X, track.Y + (int)thumbY, track.Width, (int)thumbH);
            SHPCRenderer.DrawFilledRect(sb, px, track, SHPCTheme.Border * (0.25f * a));
            SHPCRenderer.DrawFilledRect(sb, px, thumb, SHPCTheme.Cyan * (0.7f * a));
        }

        private static void DrawItemIcon(SpriteBatch sb, Item item, Vector2 center, float maxSize, float a) {
            if (item == null || item.IsAir) {
                return;
            }
            Main.instance.LoadItem(item.type);
            Texture2D tex = TextureAssets.Item[item.type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[item.type] != null
                ? Main.itemAnimations[item.type].GetFrame(tex)
                : tex.Bounds;
            float tw = frame.Width;
            float th = frame.Height;
            float scale = MathF.Min(maxSize / tw, maxSize / th);
            if (scale > 1f) scale = 1f;

            if (item.ModItem is SHPCModuleItem mod
                && SHPCModuleRender.Begin(sb, mod.TintColor,
                    new Vector2(tex.Width, tex.Height), Main.UIScaleMatrix, mod.TintIntensity)) {
                sb.Draw(tex, center, frame, Color.White * a, 0f,
                    new Vector2(tw * 0.5f, th * 0.5f), scale, SpriteEffects.None, 0f);
                SHPCModuleRender.End(sb);
            }
            else {
                sb.Draw(tex, center, frame, Color.White * a, 0f,
                    new Vector2(tw * 0.5f, th * 0.5f), scale, SpriteEffects.None, 0f);
            }
        }
    }
}
