using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.EntrustManager.Styles
{
    /// <summary>森林样式，ForestPanel 底</summary>
    internal class ForestManagerStyle : BaseManagerStyle
    {
        #region 动画计时器

        private float shaderTime;
        private float magicTimer;
        private float runeTimer;
        private float glowTimer;
        private const int EdgePad = 12;

        private struct LeafParticle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rot;
            public int Type; // 0=叶片 1=孢子 2=光尘
        }
        private readonly List<LeafParticle> leafParticles = [];

        #endregion

        #region 色板

        private static readonly Color PrimaryBright = new(180, 255, 160);
        private static readonly Color PrimaryMid = new(100, 180, 90);
        private static readonly Color PrimaryDim = new(55, 110, 50);
        private static readonly Color AccentGlow = new(140, 255, 180);
        private static readonly Color AccentGold = new(255, 220, 100);
        private static readonly Color BgDeep = new(10, 14, 5);
        private static readonly Color StatusComplete = new(100, 200, 120);
        private static readonly Color StatusFailed = new(220, 60, 70);
        private static readonly Color StatusSuspended = new(160, 150, 90);

        #endregion

        #region 生命周期

        public override void Update(Rectangle panelRect, float openProgress) {
            base.Update(panelRect, openProgress);

            shaderTime += 0.004f;
            if (shaderTime > 100f) shaderTime -= 100f;

            magicTimer += 0.02f;
            if (magicTimer > MathHelper.TwoPi) magicTimer -= MathHelper.TwoPi;

            runeTimer += 0.03f;
            if (runeTimer > MathHelper.TwoPi) runeTimer -= MathHelper.TwoPi;

            glowTimer += 0.025f;
            if (glowTimer > MathHelper.TwoPi) glowTimer -= MathHelper.TwoPi;

            if (openProgress > 0.3f && Main.rand.NextBool(7)) {
                float life = Main.rand.NextFloat(80f, 160f);
                leafParticles.Add(new LeafParticle {
                    Pos = new Vector2(Main.rand.NextFloat(0, 1f), Main.rand.NextFloat(0, 1f)),
                    Vel = new Vector2(Main.rand.NextFloat(0.0005f, 0.002f), Main.rand.NextFloat(0.001f, 0.003f)),
                    Life = life,
                    MaxLife = life,
                    Size = Main.rand.NextFloat(1.5f, 3f),
                    Rot = Main.rand.NextFloat(0, MathHelper.TwoPi),
                    Type = Main.rand.Next(3)
                });
            }
            for (int i = leafParticles.Count - 1; i >= 0; i--) {
                var p = leafParticles[i];
                p.Life -= 1f;
                p.Pos += p.Vel;
                p.Rot += 0.02f;
                leafParticles[i] = p;
                if (p.Life <= 0) leafParticles.RemoveAt(i);
            }
        }

        public override void Reset() {
            base.Reset();
            shaderTime = 0f;
            magicTimer = 0f;
            runeTimer = 0f;
            glowTimer = 0f;
            leafParticles.Clear();
        }

        #endregion

        #region 着色器面板背景

        //ForestPanel 底图，降级手绘
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            if (EffectLoader.ForestPanel?.Value != null) {
                Effect effect = EffectLoader.ForestPanel.Value;
                Rectangle extRect = rect;
                extRect.Inflate(EdgePad, EdgePad);

                effect.Parameters["uTime"]?.SetValue(shaderTime);
                effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                effect.Parameters["uNightMode"]?.SetValue(0f);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);

                sb.Draw(Px, extRect, Color.White);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                DrawFallbackBackground(sb, rect, alpha);
            }
        }

        private void DrawFallbackBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Color top = new(22, 28, 12);
            Color bot = new(10, 14, 5);

            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1f) / segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                float noise = MathF.Sin(t * 50f + magicTimer * 0.5f) * 0.15f + 0.85f;
                Color c = Color.Lerp(top, bot, t) * noise;
                FillRect(sb, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * alpha * 0.9f);
            }

            for (int i = 0; i < 40; i++) {
                float t = i / 40f;
                int y = rect.Y + (int)(t * rect.Height);
                float scan = MathF.Sin(t * 80f + magicTimer) * 0.5f + 0.5f;
                HLine(sb, rect.X, y, rect.Width, Color.Black * (alpha * scan * 0.08f));
            }

            int vigW = rect.Width / 4;
            for (int v = 0; v < vigW; v += 3) {
                float fade = 1f - v / (float)vigW;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.15f * fade);
                FillRect(sb, new Rectangle(rect.X + v, rect.Y, 2, rect.Height), vc);
                FillRect(sb, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height), vc);
            }

            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            Color pulseC = new(60, 120, 40);
            FillRect(sb, rect, pulseC * (0.03f * pulse * alpha));
        }

        #endregion

        #region 面板背景

        public override void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            DrawShadowLayers(sb, rect, alpha, 10, 4, 5);
            DrawShaderPanel(sb, rect, alpha);

            float runePulse = MathF.Sin(runeTimer) * 0.5f + 0.5f;
            Color runeColor = new Color(120, 200, 80) * ((0.4f + runePulse * 0.3f) * alpha);
            int runeOff = 16;
            DrawRuneNode(sb, new Vector2(rect.X + runeOff, rect.Y + runeOff), 10, runeColor, runeTimer);
            DrawRuneNode(sb, new Vector2(rect.Right - runeOff, rect.Y + runeOff), 10, runeColor, runeTimer + MathHelper.PiOver2);
            DrawRuneNode(sb, new Vector2(rect.X + runeOff, rect.Bottom - runeOff), 10, runeColor, runeTimer + MathHelper.Pi);
            DrawRuneNode(sb, new Vector2(rect.Right - runeOff, rect.Bottom - runeOff), 10, runeColor, runeTimer + MathHelper.Pi * 1.5f);

            Color vineColor = new Color(80, 160, 80) * (alpha * 0.3f);
            int traceLen = 35;
            DrawVineTrace(sb, new Vector2(rect.X + runeOff, rect.Y + runeOff), traceLen, 0f, vineColor);
            DrawVineTrace(sb, new Vector2(rect.X + runeOff, rect.Y + runeOff), traceLen, MathHelper.PiOver2, vineColor);
            DrawVineTrace(sb, new Vector2(rect.Right - runeOff, rect.Y + runeOff), traceLen, MathHelper.PiOver2, vineColor);
            DrawVineTrace(sb, new Vector2(rect.Right - runeOff, rect.Y + runeOff), traceLen, MathHelper.Pi, vineColor);
        }

        private void DrawRuneNode(SpriteBatch sb, Vector2 center, float size, Color color, float rotation) {
            int points = 6;
            for (int i = 0; i < points; i++) {
                float a1 = i / (float)points * MathHelper.TwoPi + rotation;
                float a2 = (i + 2) / (float)points * MathHelper.TwoPi + rotation;
                Vector2 p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * size;
                Vector2 p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * size;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                sb.Draw(Px, p1, new Rectangle(0, 0, (int)len, 1), color, rot, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
            sb.Draw(Px, center, new Rectangle(0, 0, 4, 4), color * 1.2f, 0f,
                new Vector2(2, 2), 1f, SpriteEffects.None, 0f);
        }

        private void DrawVineTrace(SpriteBatch sb, Vector2 start, int length, float angle, Color color) {
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
            Vector2 perp = new(-dir.Y, dir.X);
            for (int i = 0; i < length; i += 3) {
                float t = i / (float)length;
                float wave = MathF.Sin(t * MathHelper.TwoPi * 2f + magicTimer) * 2.5f;
                Vector2 pos = start + dir * i + perp * wave;
                float fade = 1f - t;
                sb.Draw(Px, pos, new Rectangle(0, 0, 3, 2), color * fade, angle, Vector2.One, 1f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        #region 面板边框

        public override void DrawPanelFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;
            Color outer = new(80, 140, 90);
            Color inner = new(120, 200, 130);
            Color edgeC = Color.Lerp(outer, inner, pulse) * alpha;

            HLine(sb, rect.X, rect.Y, rect.Width, edgeC);
            HLine(sb, rect.X, rect.Y + 1, rect.Width, edgeC * 0.7f);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, edgeC * 0.6f);
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * 0.8f);
            VLine(sb, rect.X + 1, rect.Y, rect.Height, edgeC * 0.5f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, edgeC * 0.5f);

            int cs = 8;
            Color cornerC = AccentGlow * (alpha * 0.6f);
            HLine(sb, rect.X, rect.Y, cs, cornerC);
            VLine(sb, rect.X, rect.Y, cs, cornerC);
            HLine(sb, rect.Right - cs, rect.Y, cs, cornerC);
            VLine(sb, rect.Right - 1, rect.Y, cs, cornerC);
            HLine(sb, rect.X, rect.Bottom - 1, cs, cornerC);
            VLine(sb, rect.X, rect.Bottom - cs, cs, cornerC);
            HLine(sb, rect.Right - cs, rect.Bottom - 1, cs, cornerC);
            VLine(sb, rect.Right - 1, rect.Bottom - cs, cs, cornerC);
        }

        #endregion

        #region 标题栏

        public override void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            Color headerBg = new(12, 18, 6);
            FillRect(sb, headerRect, headerBg * (alpha * 0.75f));

            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1) / 4f;
                int y1 = headerRect.Y + (int)(t * headerRect.Height);
                int y2 = headerRect.Y + (int)(t2 * headerRect.Height);
                Color gradC = Color.Lerp(PrimaryDim * 0.12f, Color.Transparent, t);
                FillRect(sb, new Rectangle(headerRect.X, y1, headerRect.Width, Math.Max(1, y2 - y1)), gradC * alpha);
            }

            Vector2 iconCenter = new(headerRect.X + 22f, headerRect.Y + headerRect.Height / 2f);
            float runeSpin = runeTimer * 0.5f;
            DrawRuneNode(sb, iconCenter, 8, AccentGlow * (alpha * 0.45f), runeSpin);

            var font = FontAssets.MouseText.Value;
            float headerBlink = MathF.Sin(glowTimer) * 0.1f + 0.9f;
            Vector2 titlePos = new(headerRect.X + 40f, headerRect.Y + (headerRect.Height - 18f) / 2f);
            float maxHeaderTitleW = headerRect.Width - 110f;
            if (font.MeasureString(title).X * 1.0f > maxHeaderTitleW) {
                while (title.Length > 3 && font.MeasureString(title + "...").X * 1.0f > maxHeaderTitleW)
                    title = title[..^1];
                title += "...";
            }
            Utils.DrawBorderString(sb, title, titlePos, PrimaryBright * (alpha * headerBlink), 1.0f);

            float tagBlink = MathF.Sin(glowTimer * 1.6f) * 0.3f + 0.7f;
            string tag = QuestManagerUI.HeaderStatusTag.Value;
            float tagW = font.MeasureString(tag).X * 0.6f;
            Utils.DrawBorderString(sb, tag,
                new Vector2(headerRect.Right - tagW - 14f, headerRect.Y + (headerRect.Height - 14f) / 2f),
                AccentGlow * (alpha * 0.5f * tagBlink), 0.6f);

            int lineW = headerRect.Width - 16;
            int lineX = headerRect.X + 8;
            int lineY = headerRect.Bottom - 2;
            HLine(sb, lineX, lineY, lineW, Color.Black * (alpha * 0.6f));
            HLine(sb, lineX, lineY + 1, lineW, PrimaryDim * (alpha * 0.4f));
            float sweepX = lineX + magicTimer * 0.4f % MathHelper.TwoPi / MathHelper.TwoPi * lineW;
            for (int dx = -4; dx <= 4; dx++) {
                int px = (int)sweepX + dx;
                if (px >= lineX && px < lineX + lineW) {
                    float f = 1f - Math.Abs(dx) / 5f;
                    FillRect(sb, new Rectangle(px, lineY, 1, 2), AccentGlow * (alpha * 0.5f * f * f));
                }
            }
        }

        #endregion

        #region 分类选项卡

        public override void DrawCategoryTabs(SpriteBatch sb, Rectangle tabRect, string[] categories,
            int selectedIndex, float alpha) {
            var font = FontAssets.MouseText.Value;
            float scale = 0.72f;
            float tabX = tabRect.X + 6f;

            for (int i = 0; i < categories.Length; i++) {
                string label = categories[i];
                Vector2 size = font.MeasureString(label) * scale;
                float tabW = size.X + 18f;
                Rectangle tab = new((int)tabX, tabRect.Y + 2, (int)tabW, tabRect.Height - 4);

                bool selected = i == selectedIndex;

                Color topC = selected ? new(50, 90, 40) : new(20, 32, 14);
                Color botC = selected ? new(30, 55, 25) : new(12, 20, 8);
                for (int seg = 0; seg < 4; seg++) {
                    float t = seg / 4f;
                    float t2 = (seg + 1) / 4f;
                    int y1 = tab.Y + (int)(t * tab.Height);
                    int y2 = tab.Y + (int)(t2 * tab.Height);
                    FillRect(sb, new Rectangle(tab.X, y1, tab.Width, Math.Max(1, y2 - y1)),
                        Color.Lerp(topC, botC, t) * (alpha * 0.8f));
                }

                if (selected) {
                    HLine(sb, tab.X, tab.Bottom - 1, tab.Width, 2, AccentGlow * (alpha * 0.85f));
                    HLine(sb, tab.X + 2, tab.Y, tab.Width - 4, Color.White * (alpha * 0.08f));
                }

                if (selected) {
                    Color edgeC = PrimaryMid * (alpha * 0.4f);
                    VLine(sb, tab.X, tab.Y, tab.Height, edgeC);
                    VLine(sb, tab.Right - 1, tab.Y, tab.Height, Color.Black * (alpha * 0.15f));
                    HLine(sb, tab.X, tab.Y, tab.Width, edgeC * 0.7f);
                }

                Color textC = selected ? PrimaryBright * alpha : PrimaryMid * (alpha * 0.65f);
                Utils.DrawBorderString(sb, label,
                    new Vector2(tab.X + (tab.Width - size.X) / 2f, tab.Y + (tab.Height - size.Y) / 2f),
                    textC, scale);

                tabX += tabW + 3f;
            }

            HLine(sb, tabRect.X, tabRect.Bottom - 1, tabRect.Width, PrimaryDim * (alpha * 0.25f));
        }

        #endregion

        #region 滚动条

        public override void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha) {
            FillRect(sb, trackRect, new Color(8, 12, 4) * (alpha * 0.5f));
            VLine(sb, trackRect.X, trackRect.Y, trackRect.Height, PrimaryDim * (alpha * 0.2f));

            float clampedView = MathHelper.Clamp(viewRatio, 0.1f, 1f);
            int thumbH = Math.Max(20, (int)(trackRect.Height * clampedView));
            int thumbY = trackRect.Y + (int)((trackRect.Height - thumbH) * MathHelper.Clamp(scrollRatio, 0f, 1f));
            Rectangle thumb = new(trackRect.X + 1, thumbY, trackRect.Width - 2, thumbH);

            float thumbPulse = MathF.Sin(pulseTimer * 1.5f) * 0.15f + 0.55f;
            for (int ty = thumb.Y; ty < thumb.Bottom; ty++) {
                float t = (ty - thumb.Y) / (float)thumb.Height;
                Color c = Color.Lerp(PrimaryMid * 0.6f, PrimaryDim * 0.3f, t);
                HLine(sb, thumb.X, ty, thumb.Width, c * (alpha * thumbPulse));
            }

            HLine(sb, thumb.X, thumb.Y, thumb.Width, Color.White * (alpha * 0.1f));

            float flow = magicTimer * 0.3f % MathHelper.TwoPi / MathHelper.TwoPi;
            int flowY = thumb.Y + (int)(flow * thumb.Height);
            if (flowY >= thumb.Y && flowY < thumb.Bottom)
                HLine(sb, thumb.X, flowY, thumb.Width, AccentGlow * (alpha * 0.2f));

            DrawNatureBorder(sb, thumb, PrimaryMid * (alpha * 0.35f));
        }

        #endregion

        #region 底部状态栏

        public override void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha) {
            FillRect(sb, footerRect, new Color(10, 14, 5) * (alpha * 0.65f));

            HLine(sb, footerRect.X + 8, footerRect.Y, footerRect.Width - 16, Color.Black * (alpha * 0.5f));
            HLine(sb, footerRect.X + 8, footerRect.Y + 1, footerRect.Width - 16, PrimaryDim * (alpha * 0.3f));

            string statsText = string.Format(QuestManagerUI.FooterStatsFormat.Value, totalQuests, activeQuests);
            float statsBlink = MathF.Sin(globalTimer * 1.2f) * 0.1f + 0.9f;
            Utils.DrawBorderString(sb, statsText,
                new Vector2(footerRect.X + 12f, footerRect.Y + (footerRect.Height - 12f) / 2f),
                PrimaryMid * (alpha * 0.7f * statsBlink), 0.62f);

            string verTag = CWRMod.Instance.Version.ToString();
            var font = FontAssets.MouseText.Value;
            float vw = font.MeasureString(verTag).X * 0.55f;
            Utils.DrawBorderString(sb, verTag,
                new Vector2(footerRect.Right - vw - 10f, footerRect.Y + (footerRect.Height - 12f) / 2f),
                PrimaryDim * (alpha * 0.4f), 0.55f);
        }

        #endregion

        #region 任务条目

        public override void DrawQuestEntry(SpriteBatch sb, Rectangle entryRect, EntrustEntryData entry,
            bool isSelected, bool isHovered, float alpha, int entryIndex) {
            var font = FontAssets.MouseText.Value;

            if (isSelected) {
                FillRect(sb, entryRect, PrimaryDim * (alpha * 0.2f));
                DrawNatureBorder(sb, entryRect, AccentGlow * (alpha * 0.4f));
            }
            else if (isHovered) {
                FillRect(sb, entryRect, PrimaryDim * (alpha * 0.1f));
                DrawNatureBorder(sb, entryRect, PrimaryMid * (alpha * 0.2f));
            }

            Color statusBarColor = GetStatusColor(entry.Status, alpha);
            VLine(sb, entryRect.X + 2, entryRect.Y + 4, entryRect.Height - 8, 3, statusBarColor);

            int nodeX = entryRect.X + 16;
            int nodeY = entryRect.Y + 14;
            DrawLeafNode(sb, new Vector2(nodeX, nodeY), entry.Status, alpha, entryIndex);

            float titleX = entryRect.X + 28f;
            float titleY = entryRect.Y + 6f;

            Color titleColor = entry.Status == QuestEntryStatus.Completed
                ? StatusComplete * (alpha * 0.75f)
                : PrimaryBright * alpha;
            if (entry.IsNew) {
                float newBlink = MathF.Sin(glowTimer * 4f) * 0.3f + 0.7f;
                titleColor = Color.Lerp(titleColor, AccentGold, newBlink * 0.4f);
            }

            //截断标题，预留标签宽；有提供者徽记时再让一枚戳位
            string statusText = GetEntryStatusText(entry.Status);
            float statusBadgeScale = 0.55f;
            int statusBadgeW = GetStatusBadgeWidth(statusText, statusBadgeScale);
            float statusBadgeX = entryRect.Right - statusBadgeW - (entry.Provider != null ? 58f : 28f);
            string displayTitle = entry.Title ?? "";
            float maxEntryTitleW = Math.Max(40f, statusBadgeX - titleX - 8f);
            if (font.MeasureString(displayTitle).X * 0.90f > maxEntryTitleW) {
                while (displayTitle.Length > 3 && font.MeasureString(displayTitle + "...").X * 0.90f > maxEntryTitleW)
                    displayTitle = displayTitle[..^1];
                displayTitle += "...";
            }
            Utils.DrawBorderString(sb, displayTitle, new Vector2(titleX, titleY), titleColor, 0.90f);
            Rectangle statusBadgeRect = new((int)statusBadgeX, entryRect.Y + 8, statusBadgeW, 15);
            DrawForestStatusBadge(sb, statusBadgeRect, statusText, entry.Status,
                alpha, statusBadgeScale, entryIndex);

            if (entry.Status == QuestEntryStatus.Tracked) {
                float leafBlink = MathF.Sin(glowTimer * 3f) * 0.4f + 0.6f;
                float titleW = font.MeasureString(displayTitle).X * 0.78f;
                Color leafColor = AccentGlow * (alpha * leafBlink);
                if (titleX + titleW + 18f < statusBadgeX) {
                    Utils.DrawBorderString(sb, "✦",
                        new Vector2(titleX + titleW + 6f, titleY + 1f),
                        leafColor, 0.65f);
                }
            }

            float summaryY = titleY + 20f;
            Color summaryColor = PrimaryMid * (alpha * 0.6f);
            string summary = (entry.Summary ?? "").Replace("\r", "").Replace("\n", " ").Trim();
            float maxSummaryW = entryRect.Width - 50f;
            if (font.MeasureString(summary).X * 0.72f > maxSummaryW) {
                while (summary.Length > 3 && font.MeasureString(summary + "...").X * 0.72f > maxSummaryW)
                    summary = summary[..^1];
                summary += "...";
            }
            float collapsedAlpha = 1f - entry.ExpandProgress;
            if (collapsedAlpha > 0.01f) {
                Utils.DrawBorderString(sb, summary, new Vector2(titleX, summaryY),
                    summaryColor * collapsedAlpha, 0.72f);
            }

            if (!string.IsNullOrEmpty(entry.Summary)) {
                string expandIcon = entry.IsExpanded ? "▲" : "▼";
                float iconAlpha = isHovered ? 0.7f : 0.35f;
                float iconX = entryRect.Right - 18f;
                Utils.DrawBorderString(sb, expandIcon, new Vector2(iconX, titleY + 2f),
                    PrimaryMid * (alpha * iconAlpha), 0.6f);
            }

            if (entry.ExpandProgress > 0.01f) {
                int baseH = GetEntryHeight();
                float expandAlpha = alpha * entry.ExpandProgress;
                float descY = entryRect.Y + baseH - 4f;

                int expandAreaH = entryRect.Height - baseH;
                if (expandAreaH > 0) {
                    const int bgLeftPad = 22;
                    Rectangle expandBg = new(entryRect.X + bgLeftPad, (int)descY - 2, entryRect.Width - bgLeftPad - 4, expandAreaH + 2);
                    FillRect(sb, expandBg, new Color(8, 14, 4) * (expandAlpha * 0.35f));
                }

                int sepW = (int)(entryRect.Width - titleX + entryRect.X - 14f);
                HLine(sb, (int)titleX, (int)descY, sepW, Color.Black * (expandAlpha * 0.5f));
                HLine(sb, (int)titleX, (int)descY + 1, sepW, PrimaryDim * (expandAlpha * 0.3f));
                float sepSweep = (int)titleX + magicTimer * 0.3f % MathHelper.TwoPi / MathHelper.TwoPi * sepW;
                for (int dx = -3; dx <= 3; dx++) {
                    int spx = (int)sepSweep + dx;
                    if (spx >= (int)titleX && spx < (int)titleX + sepW) {
                        float f = 1f - Math.Abs(dx) / 4f;
                        FillRect(sb, new Rectangle(spx, (int)descY, 1, 2), AccentGlow * (expandAlpha * 0.4f * f * f));
                    }
                }
                descY += 6f;

                string fullText = entry.Summary ?? "";
                float descScale = 0.70f;
                int wrapWidth = (int)((entryRect.Width - 40f) / descScale);
                Color descColor = new Color(200, 220, 180) * (expandAlpha * 0.75f);

                //正文给落款让出底部
                int sigH = GetProviderSignatureHeight(entry);
                float textBottom = entryRect.Bottom - 4f - sigH;
                string[] paragraphs = fullText.Split('\n');
                foreach (string paragraph in paragraphs) {
                    string trimmedPara = paragraph.Trim();
                    if (string.IsNullOrEmpty(trimmedPara)) continue;
                    string[] wrapped = VaultUtils.WrapTextArray(trimmedPara, font, wrapWidth, 99, out _);
                    foreach (string wl in wrapped) {
                        if (string.IsNullOrEmpty(wl)) continue;
                        if (descY > textBottom) break;
                        string trimmed = wl.TrimEnd('-', ' ');
                        Utils.DrawBorderString(sb, trimmed, new Vector2(titleX, descY), descColor, descScale);
                        descY += (int)(font.MeasureString(trimmed).Y * descScale) + 2;
                    }
                }

                if (sigH > 0) {
                    DrawProviderSignature(sb, entry, titleX, entryRect.Bottom - sigH,
                        entryRect.Width - (titleX - entryRect.X) - 12f, expandAlpha);
                }
            }

            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                float barY = entry.ExpandProgress > 0.5f
                    ? entryRect.Bottom - 14f
                    : summaryY + 18f;
                int barW = Math.Min(120, entryRect.Width - 50);
                Rectangle barRect = new((int)titleX, (int)barY, barW, 5);
                DrawProgressBar(sb, barRect, entry.Progress,
                    new Color(8, 12, 4) * alpha,
                    PrimaryMid * alpha, AccentGlow * alpha,
                    PrimaryDim * (alpha * 0.4f), pulseTimer);

                float flow = magicTimer * 0.3f % MathHelper.TwoPi / MathHelper.TwoPi;
                int fillW = (int)(barW * MathHelper.Clamp(entry.Progress, 0f, 1f));
                int flowX = barRect.X + (int)(flow * fillW);
                if (fillW > 2 && flowX < barRect.X + fillW)
                    FillRect(sb, new Rectangle(flowX, barRect.Y, 2, barRect.Height), AccentGlow * (alpha * 0.3f));

                if (entry.ProgressText != null) {
                    Utils.DrawBorderString(sb, entry.ProgressText,
                        new Vector2(barRect.Right + 6f, barRect.Y - 2f),
                        PrimaryMid * (alpha * 0.55f), 0.55f);
                }
            }

            //提供者徽记钉在行右缘，展开图标左侧
            if (entry.Provider != null) {
                DrawProviderBadge(sb, new Vector2(entryRect.Right - 36f, entryRect.Y + GetEntryHeight() * 0.5f),
                    13f, entry, alpha);
            }
        }

        private void DrawForestStatusBadge(SpriteBatch sb, Rectangle badgeRect, string statusText,
            QuestEntryStatus status, float alpha, float scale, int entryIndex) {
            if (badgeRect.Width <= 0 || string.IsNullOrEmpty(statusText)) return;

            Color statusColor = GetStatusColor(status, 1f);
            float glow = MathF.Sin(glowTimer * 1.8f + entryIndex * 0.4f) * 0.16f + 0.84f;
            FillRect(sb, badgeRect, BgDeep * (alpha * 0.46f));
            FillRect(sb, new Rectangle(badgeRect.X + 1, badgeRect.Y + 1,
                badgeRect.Width - 2, badgeRect.Height - 2),
                Color.Lerp(PrimaryDim, statusColor, 0.28f) * (alpha * 0.22f));

            HLine(sb, badgeRect.X + 4, badgeRect.Y, badgeRect.Width - 8,
                statusColor * (alpha * 0.28f * glow));
            HLine(sb, badgeRect.X + 3, badgeRect.Bottom - 2, badgeRect.Width - 6,
                PrimaryMid * (alpha * 0.34f));
            for (int x = badgeRect.X + 5; x < badgeRect.Right - 4; x += 7) {
                float leafWave = MathF.Sin(magicTimer + x * 0.07f) * 0.5f;
                sb.Draw(Px, new Vector2(x, badgeRect.Bottom - 2 + leafWave),
                    null, statusColor * (alpha * 0.18f), 0.25f,
                    new Vector2(0.5f), new Vector2(2.6f, 1.1f), SpriteEffects.None, 0f);
            }

            DrawSmallHex(sb, new Vector2(badgeRect.X + 5f, badgeRect.Y + badgeRect.Height / 2f),
                3.2f, statusColor * (alpha * 0.72f * glow));
            Utils.DrawBorderString(sb, statusText,
                new Vector2(badgeRect.X + 10f, badgeRect.Y + 1f),
                statusColor * (alpha * (status == QuestEntryStatus.Active ? 0.74f : 0.95f)), scale);
        }

        private void DrawLeafNode(SpriteBatch sb, Vector2 center, QuestEntryStatus status, float alpha, int index) {
            float pulse = MathF.Sin(pulseTimer + index * 0.8f) * 0.2f + 0.8f;
            Color nodeColor = GetStatusColor(status, alpha * pulse);

            if (status == QuestEntryStatus.Completed) {
                DrawSmallHex(sb, center, 5f, nodeColor);
                sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.25f * pulse), 0f,
                    new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
            }
            else if (status == QuestEntryStatus.Failed) {
                sb.Draw(Px, center, null, nodeColor, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(5f, 1.5f), SpriteEffects.None, 0f);
                sb.Draw(Px, center, null, nodeColor, -MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(5f, 1.5f), SpriteEffects.None, 0f);
            }
            else {
                if (status == QuestEntryStatus.Tracked) {
                    float glowPulse = MathF.Sin(glowTimer * 3f + index) * 0.3f + 0.3f;
                    sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), AccentGlow * (alpha * glowPulse * 0.15f), 0f,
                        new Vector2(0.5f), 12f, SpriteEffects.None, 0f);
                }
                DrawSmallHex(sb, center, 5f, nodeColor);
                DrawSmallHex(sb, center, 3f, BgDeep * (alpha * 0.9f));
            }
        }

        //小六角≈缩放方
        private void DrawSmallHex(SpriteBatch sb, Vector2 center, float size, Color color) {
            sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f), new Vector2(size, size * 0.85f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver4 * 0.5f,
                new Vector2(0.5f), new Vector2(size * 0.9f, size * 0.75f), SpriteEffects.None, 0f);
        }

        public override void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            int dashLen = 6, gapLen = 5;
            float x = start.X;
            int dotIndex = 0;
            while (x < end.X) {
                float segEnd = Math.Min(x + dashLen, end.X);
                if (segEnd > x)
                    HLine(sb, (int)x, (int)start.Y, (int)(segEnd - x), PrimaryDim * (alpha * 0.12f));
                if (dotIndex % 4 == 2) {
                    float midX = x + dashLen / 2f;
                    float leafRot = MathF.Sin(magicTimer + dotIndex) * 0.3f;
                    sb.Draw(Px, new Vector2(midX, start.Y), new Rectangle(0, 0, 1, 1),
                        PrimaryMid * (alpha * 0.2f), leafRot, new Vector2(0.5f),
                        new Vector2(3f, 1.5f), SpriteEffects.None, 0f);
                }
                x += dashLen + gapLen;
                dotIndex++;
            }
        }

        #endregion

        #region 颜色

        public override Color GetShadowColor(float alpha) => Color.Black * (alpha * 0.45f);

        public override Color GetHeaderTextColor(float alpha) => PrimaryBright * alpha;

        public override Color GetStatusColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Active => PrimaryMid * alpha,
                QuestEntryStatus.Tracked => AccentGlow * alpha,
                QuestEntryStatus.Suspended => StatusSuspended * alpha,
                QuestEntryStatus.Completed => StatusComplete * alpha,
                QuestEntryStatus.Failed => StatusFailed * alpha,
                _ => PrimaryDim * alpha,
            };
        }

        #endregion

        #region 粒子与特效

        public override void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha) {
            foreach (var p in leafParticles) {
                Vector2 drawPos = new(
                    panelRect.X + p.Pos.X * panelRect.Width,
                    panelRect.Y + p.Pos.Y * panelRect.Height);
                if (drawPos.X < panelRect.X || drawPos.X > panelRect.Right ||
                    drawPos.Y < panelRect.Y || drawPos.Y > panelRect.Bottom) continue;

                float fadeIn = Math.Min(1f, (p.MaxLife - p.Life) / 20f);
                float fadeOut = Math.Min(1f, p.Life / 20f);
                float fade = fadeIn * fadeOut;

                Color pColor;
                if (p.Type == 0) pColor = new Color(120, 200, 80); // 叶片
                else if (p.Type == 1) pColor = new Color(200, 220, 100); // 孢子
                else pColor = new Color(180, 255, 150); // 光尘

                pColor *= fade * 0.45f * alpha;

                sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor, p.Rot,
                    new Vector2(0.5f), new Vector2(p.Size * (p.Type == 0 ? 2f : 1.2f), p.Size),
                    SpriteEffects.None, 0f);
            }
        }

        public override void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha) {
            float flicker = MathF.Sin(globalTimer * 1.8f) * 0.5f + 0.5f;
            FillRect(sb, panelRect, new Color(30, 60, 20) * (alpha * 0.02f * flicker));

            float sweepY = panelRect.Y + shaderTime * 0.05f % 1f * panelRect.Height;
            Color sweepC = new(60, 120, 40);
            for (int dy = -3; dy <= 3; dy++) {
                int py = (int)sweepY + dy;
                if (py < panelRect.Y || py >= panelRect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 4f;
                HLine(sb, panelRect.X + 4, py, panelRect.Width - 8, sweepC * (alpha * 0.05f * f * f));
            }
        }

        #endregion

        #region 样式切换按钮

        public override Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) =>
            new(panelRect.Right - 180, panelRect.Y + 6, 26, 26);

        public override void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetStyleSwitchButtonRect(panelRect);
            DrawSmallNatureButton(sb, btnRect, isHovered, alpha, DrawLeafIcon);
        }

        private void DrawSmallNatureButton(SpriteBatch sb, Rectangle rect, bool hover, float alpha,
            Action<SpriteBatch, Vector2, float> drawIcon) {
            Rectangle shadow = rect;
            shadow.Offset(1, 2);
            FillRect(sb, shadow, Color.Black * (0.35f * alpha));

            Color topC = hover ? new Color(70, 130, 70) : new Color(45, 85, 45);
            Color botC = hover ? new Color(40, 75, 40) : new Color(25, 50, 25);
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1f) / 4f;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                FillRect(sb, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    Color.Lerp(topC, botC, t) * alpha);
            }

            HLine(sb, rect.X + 2, rect.Y, rect.Width - 4, Color.White * (0.12f * alpha));

            Color edgeC = hover ? Color.White : new Color(160, 220, 160);
            HLine(sb, rect.X, rect.Y, rect.Width, edgeC * (0.5f * alpha));
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * (0.3f * alpha));
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * (0.2f * alpha));
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, Color.Black * (0.3f * alpha));

            drawIcon?.Invoke(sb, rect.Center.ToVector2(), alpha);
        }

        private void DrawLeafIcon(SpriteBatch sb, Vector2 center, float alpha) {
            Color ic = new Color(200, 255, 180) * alpha;
            sb.Draw(Px, center + new Vector2(2, -2), new Rectangle(0, 0, 14, 18),
                ic * 0.35f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1), new Rectangle(0, 0, 14, 18),
                ic * 0.75f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, -4),
                new Rectangle(0, 0, 7, 1), new Color(80, 160, 80) * (0.5f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 0),
                new Rectangle(0, 0, 7, 1), new Color(80, 160, 80) * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 4),
                new Rectangle(0, 0, 5, 1), new Color(80, 160, 80) * (0.3f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 工具方法

        private static void DrawNatureBorder(SpriteBatch sb, Rectangle rect, Color color) {
            HLine(sb, rect.X, rect.Y, rect.Width, color);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, color * 0.5f);
            VLine(sb, rect.X, rect.Y, rect.Height, color * 0.7f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * 0.15f);
        }

        #endregion
    }
}
