using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.EntrustManager.Styles
{
    /// <summary>热风锻铁工坊任务管理器样式</summary>
    internal class HotwindManagerStyle : BaseManagerStyle
    {
        #region 动画计时器

        private float shaderTime;
        private float flowTimer;
        private float headerGlowPhase;
        private float scanTimer;
        private const int EdgePad = 12;

        //金属火花粒子
        private struct SparkParticle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public int Type; // 0=火花 1=余烬 2=亮点
        }
        private readonly List<SparkParticle> sparkParticles = [];

        #endregion

        #region 色板

        private static readonly Color PrimaryBright = new(255, 210, 140);
        private static readonly Color PrimaryMid = new(190, 110, 50);
        private static readonly Color PrimaryDim = new(120, 60, 25);
        private static readonly Color AccentCopper = new(220, 140, 60);
        private static readonly Color AccentGold = new(255, 200, 80);
        private static readonly Color BgDeep = new(18, 10, 6);
        private static readonly Color StatusComplete = new(100, 200, 80);
        private static readonly Color StatusFailed = new(220, 60, 70);
        private static readonly Color StatusSuspended = new(160, 140, 100);

        #endregion

        #region 生命周期

        public override void Update(Rectangle panelRect, float openProgress) {
            base.Update(panelRect, openProgress);

            shaderTime += 0.004f;
            if (shaderTime > 100f) shaderTime -= 100f;

            flowTimer += 0.008f;
            if (flowTimer > 1000f) flowTimer -= 1000f;

            headerGlowPhase += 0.035f;
            if (headerGlowPhase > MathHelper.TwoPi) headerGlowPhase -= MathHelper.TwoPi;

            scanTimer += 0.04f;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;

            //金属火花粒子
            if (openProgress > 0.3f && Main.rand.NextBool(8)) {
                sparkParticles.Add(new SparkParticle {
                    Pos = new Vector2(Main.rand.NextFloat(0, 1f), Main.rand.NextFloat(0, 1f)),
                    Vel = new Vector2(Main.rand.NextFloat(-0.001f, 0.001f), Main.rand.NextFloat(-0.003f, -0.001f)),
                    Life = Main.rand.NextFloat(60f, 140f),
                    MaxLife = Main.rand.NextFloat(60f, 140f),
                    Size = Main.rand.NextFloat(1f, 2.5f),
                    Type = Main.rand.Next(3)
                });
            }
            for (int i = sparkParticles.Count - 1; i >= 0; i--) {
                var p = sparkParticles[i];
                p.Life -= 1f;
                p.Pos += p.Vel;
                sparkParticles[i] = p;
                if (p.Life <= 0) sparkParticles.RemoveAt(i);
            }
        }

        public override void Reset() {
            base.Reset();
            shaderTime = 0f;
            flowTimer = 0f;
            headerGlowPhase = 0f;
            scanTimer = 0f;
            sparkParticles.Clear();
        }

        #endregion

        #region 着色器面板背景

        //HotwindPanel 着色器底图，降级手绘
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            if (EffectLoader.HotwindPanel?.Value != null) {
                Effect effect = EffectLoader.HotwindPanel.Value;
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

        //降级背景
        private void DrawFallbackBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Color top = new(28, 18, 10);
            Color mid = new(18, 10, 6);
            Color bot = new(10, 6, 4);

            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1f) / segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Color c = t < 0.5f
                    ? Color.Lerp(top, mid, t * 2f)
                    : Color.Lerp(mid, bot, (t - 0.5f) * 2f);
                FillRect(sb, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)), c * alpha);
            }

            //金色扫描线
            Color scanC = new(30, 18, 8);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                HLine(sb, rect.X + 2, y, rect.Width - 4, scanC * (alpha * 0.08f));

            //暗角
            int vigW = 30;
            for (int v = 0; v < vigW; v += 3) {
                float fade = 1f - v / (float)vigW;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.18f * fade);
                FillRect(sb, new Rectangle(rect.X + v, rect.Y, 2, rect.Height), vc);
                FillRect(sb, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height), vc);
            }
            for (int v = 0; v < 20; v += 3) {
                float fade = 1f - v / 20f;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.22f * fade);
                FillRect(sb, new Rectangle(rect.X, rect.Y + v, rect.Width, 2), vc);
                FillRect(sb, new Rectangle(rect.X, rect.Bottom - v - 2, rect.Width, 2), vc);
            }

            //脉冲光覆盖
            float pulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseC = new(160, 70, 30);
            FillRect(sb, rect, pulseC * (0.03f * pulse * alpha));

            //扫掠光带
            float scanY = rect.Y + shaderTime * 0.055f % 1f * rect.Height;
            for (int dy = -5; dy <= 5; dy++) {
                int py = (int)scanY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 6f;
                Color sc = new(120, 55, 18);
                HLine(sb, rect.X + 2, py, rect.Width - 4, sc * (alpha * 0.08f * f * f));
            }
        }

        #endregion

        #region 面板背景

        public override void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            //多层扩散阴影
            DrawShadowLayers(sb, rect, alpha, 10, 4, 5);
            //着色器驱动的面板底图
            DrawShaderPanel(sb, rect, alpha);

            //角落铆钉装饰
            float rivetPulse = MathF.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            DrawCornerRivet(sb, new Vector2(rect.X + 12, rect.Y + 12), rivetPulse, alpha);
            DrawCornerRivet(sb, new Vector2(rect.Right - 12, rect.Y + 12), rivetPulse, alpha);
            DrawCornerRivet(sb, new Vector2(rect.X + 12, rect.Bottom - 12), rivetPulse * 0.7f, alpha);
            DrawCornerRivet(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), rivetPulse * 0.7f, alpha);
        }

        //角落铆钉装饰
        private void DrawCornerRivet(SpriteBatch sb, Vector2 pos, float pulse, float alpha) {
            Color baseC = new(190, 110, 50);
            Color glowC = new(160, 80, 35);

            //外层辉光
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), glowC * (0.2f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(14f, 14f), SpriteEffects.None, 0f);
            //铆钉主体
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), baseC * (0.8f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);
            //高光点
            sb.Draw(Px, pos + new Vector2(-1, -1), new Rectangle(0, 0, 1, 1),
                Color.White * (0.3f * pulse * alpha), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(2f, 2f), SpriteEffects.None, 0f);
        }

        #endregion

        #region 面板边框

        public override void DrawPanelFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            //金属边框：顶部亮线+底部暗线+左侧暖光+右侧阴影
            Color edgeC = PrimaryBright * (alpha * 0.7f);
            HLine(sb, rect.X, rect.Y, rect.Width, edgeC);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, PrimaryDim * (alpha * 0.4f));
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * 0.6f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * (alpha * 0.3f));

            //顶部反光线
            HLine(sb, rect.X + 2, rect.Y + 1, rect.Width - 4, Color.White * (alpha * 0.08f));

            //底部阴影线
            HLine(sb, rect.X + 2, rect.Bottom - 2, rect.Width - 4, Color.Black * (alpha * 0.15f));

            //四角加厚标记
            int cs = 6;
            Color cornerC = AccentGold * (alpha * 0.8f);
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
            //标题栏深暖色背景
            Color headerBg = new(22, 12, 6);
            FillRect(sb, headerRect, headerBg * (alpha * 0.75f));

            //金属渐变叠层
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1) / 4f;
                int y1 = headerRect.Y + (int)(t * headerRect.Height);
                int y2 = headerRect.Y + (int)(t2 * headerRect.Height);
                Color gradC = Color.Lerp(PrimaryDim * 0.15f, Color.Transparent, t);
                FillRect(sb, new Rectangle(headerRect.X, y1, headerRect.Width, Math.Max(1, y2 - y1)), gradC * alpha);
            }

            //锻锤图标装饰（标题左侧）
            Vector2 iconCenter = new(headerRect.X + 22f, headerRect.Y + headerRect.Height / 2f);
            sb.Draw(Px, iconCenter, new Rectangle(0, 0, 1, 1), AccentGold * (alpha * 0.6f), 0f,
                new Vector2(0.5f), new Vector2(8f, 3f), SpriteEffects.None, 0f);
            sb.Draw(Px, iconCenter, new Rectangle(0, 0, 1, 1), PrimaryMid * (alpha * 0.7f), MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(6f, 2f), SpriteEffects.None, 0f);
            sb.Draw(Px, iconCenter + new Vector2(0, -1), new Rectangle(0, 0, 1, 1),
                Color.White * (alpha * 0.25f), 0f, new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);

            //标题文字
            var font = FontAssets.MouseText.Value;
            float headerBlink = MathF.Sin(headerGlowPhase) * 0.1f + 0.9f;
            Vector2 titlePos = new(headerRect.X + 40f, headerRect.Y + (headerRect.Height - 18f) / 2f);
            float maxHeaderTitleW = headerRect.Width - 110f;
            if (font.MeasureString(title).X * 1.0f > maxHeaderTitleW) {
                while (title.Length > 3 && font.MeasureString(title + "...").X * 1.0f > maxHeaderTitleW)
                    title = title[..^1];
                title += "...";
            }
            Utils.DrawBorderString(sb, title, titlePos, PrimaryBright * (alpha * headerBlink), 1.0f);

            //右侧状态标签
            float tagBlink = MathF.Sin(headerGlowPhase * 1.6f) * 0.3f + 0.7f;
            string tag = QuestManagerUI.HeaderStatusTag.Value;
            float tagW = font.MeasureString(tag).X * 0.6f;
            Utils.DrawBorderString(sb, tag,
                new Vector2(headerRect.Right - tagW - 14f, headerRect.Y + (headerRect.Height - 14f) / 2f),
                AccentCopper * (alpha * 0.55f * tagBlink), 0.6f);

            //底部分隔线（金属凹槽 + 暖色亮点扫掠）
            int lineW = headerRect.Width - 16;
            int lineX = headerRect.X + 8;
            int lineY = headerRect.Bottom - 2;
            HLine(sb, lineX, lineY, lineW, Color.Black * (alpha * 0.7f));
            HLine(sb, lineX, lineY + 1, lineW, PrimaryDim * (alpha * 0.45f));
            //扫掠亮点
            float sweepX = lineX + flowTimer * 0.6f % 1f * lineW;
            for (int dx = -4; dx <= 4; dx++) {
                int px = (int)sweepX + dx;
                if (px >= lineX && px < lineX + lineW) {
                    float f = 1f - Math.Abs(dx) / 5f;
                    FillRect(sb, new Rectangle(px, lineY, 1, 2), AccentGold * (alpha * 0.5f * f * f));
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

                //金属渐变底色
                Color topC = selected ? new(90, 55, 25) : new(35, 20, 10);
                Color botC = selected ? new(55, 30, 14) : new(20, 12, 6);
                for (int seg = 0; seg < 4; seg++) {
                    float t = seg / 4f;
                    float t2 = (seg + 1) / 4f;
                    int y1 = tab.Y + (int)(t * tab.Height);
                    int y2 = tab.Y + (int)(t2 * tab.Height);
                    FillRect(sb, new Rectangle(tab.X, y1, tab.Width, Math.Max(1, y2 - y1)),
                        Color.Lerp(topC, botC, t) * (alpha * 0.8f));
                }

                if (selected) {
                    //选中态底部高亮线
                    HLine(sb, tab.X, tab.Bottom - 1, tab.Width, 2, AccentGold * (alpha * 0.85f));
                    //顶部反光
                    HLine(sb, tab.X + 2, tab.Y, tab.Width - 4, Color.White * (alpha * 0.1f));
                }

                //边线
                if (selected) {
                    Color edgeC = AccentCopper * (alpha * 0.5f);
                    VLine(sb, tab.X, tab.Y, tab.Height, edgeC);
                    VLine(sb, tab.Right - 1, tab.Y, tab.Height, Color.Black * (alpha * 0.2f));
                    HLine(sb, tab.X, tab.Y, tab.Width, edgeC * 0.8f);
                }

                Color textC = selected ? PrimaryBright * alpha : PrimaryMid * (alpha * 0.65f);
                Utils.DrawBorderString(sb, label,
                    new Vector2(tab.X + (tab.Width - size.X) / 2f, tab.Y + (tab.Height - size.Y) / 2f),
                    textC, scale);

                tabX += tabW + 3f;
            }

            //整行底部线
            HLine(sb, tabRect.X, tabRect.Bottom - 1, tabRect.Width, PrimaryDim * (alpha * 0.25f));
        }

        #endregion

        #region 滚动条

        public override void DrawScrollbar(SpriteBatch sb, Rectangle trackRect, float scrollRatio,
            float viewRatio, float alpha) {
            //轨道暗底
            FillRect(sb, trackRect, new Color(12, 7, 4) * (alpha * 0.5f));
            VLine(sb, trackRect.X, trackRect.Y, trackRect.Height, PrimaryDim * (alpha * 0.2f));

            //滑块
            float clampedView = MathHelper.Clamp(viewRatio, 0.1f, 1f);
            int thumbH = Math.Max(20, (int)(trackRect.Height * clampedView));
            int thumbY = trackRect.Y + (int)((trackRect.Height - thumbH) * MathHelper.Clamp(scrollRatio, 0f, 1f));
            Rectangle thumb = new(trackRect.X + 1, thumbY, trackRect.Width - 2, thumbH);

            //金属渐变填充
            float thumbPulse = MathF.Sin(pulseTimer * 1.5f) * 0.15f + 0.55f;
            for (int ty = thumb.Y; ty < thumb.Bottom; ty++) {
                float t = (ty - thumb.Y) / (float)thumb.Height;
                Color c = Color.Lerp(PrimaryMid * 0.7f, PrimaryDim * 0.35f, t);
                HLine(sb, thumb.X, ty, thumb.Width, c * (alpha * thumbPulse));
            }

            //薄顶部高光
            HLine(sb, thumb.X, thumb.Y, thumb.Width, Color.White * (alpha * 0.12f));

            //流光
            float flow = flowTimer * 3f % 1f;
            int flowY = thumb.Y + (int)(flow * thumb.Height);
            if (flowY >= thumb.Y && flowY < thumb.Bottom)
                HLine(sb, thumb.X, flowY, thumb.Width, AccentGold * (alpha * 0.25f));

            //金属边框
            DrawMetallicBorder(sb, thumb, PrimaryMid * (alpha * 0.4f));
        }

        #endregion

        #region 底部状态栏

        public override void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha) {
            //背景
            FillRect(sb, footerRect, new Color(16, 9, 5) * (alpha * 0.65f));

            //顶部分隔（金属凹槽）
            HLine(sb, footerRect.X + 8, footerRect.Y, footerRect.Width - 16, Color.Black * (alpha * 0.5f));
            HLine(sb, footerRect.X + 8, footerRect.Y + 1, footerRect.Width - 16, PrimaryDim * (alpha * 0.3f));

            //统计文本
            string statsText = string.Format(QuestManagerUI.FooterStatsFormat.Value, totalQuests, activeQuests);
            float statsBlink = MathF.Sin(globalTimer * 1.2f) * 0.1f + 0.9f;
            Utils.DrawBorderString(sb, statsText,
                new Vector2(footerRect.X + 12f, footerRect.Y + (footerRect.Height - 12f) / 2f),
                PrimaryMid * (alpha * 0.7f * statsBlink), 0.62f);

            //右侧版本标记
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
            var customStyle = entry.EntryStyle;

            //=== 背景 ===
            bool bgHandled = customStyle?.DrawEntryBackground(sb, entryRect, entry, isSelected, isHovered, alpha) ?? false;
            if (!bgHandled) {
                if (isSelected) {
                    FillRect(sb, entryRect, PrimaryDim * (alpha * 0.2f));
                    DrawMetallicBorder(sb, entryRect, AccentCopper * (alpha * 0.45f));
                }
                else if (isHovered) {
                    FillRect(sb, entryRect, PrimaryDim * (alpha * 0.1f));
                    DrawMetallicBorder(sb, entryRect, PrimaryMid * (alpha * 0.2f));
                }

                //状态指示条（左侧3px竖条，暖金属色）
                Color statusBarColor = GetStatusColor(entry.Status, alpha);
                VLine(sb, entryRect.X + 2, entryRect.Y + 4, entryRect.Height - 8, 3, statusBarColor);

                //状态铆钉节点
                int nodeX = entryRect.X + 16;
                int nodeY = entryRect.Y + 14;
                DrawStatusRivet(sb, new Vector2(nodeX, nodeY), entry.Status, alpha, entryIndex);
            }

            //=== 图标 + 标题 ===
            float titleX = entryRect.X + (bgHandled ? 10f : 28f);
            float titleY = entryRect.Y + 6f;

            //自定义图标
            float iconOffset = customStyle?.DrawEntryIcon(sb, new Vector2(titleX, titleY), entry, alpha) ?? 0f;
            titleX += iconOffset;

            Color titleColor = customStyle?.GetTitleColor(entry.Status, alpha)
                ?? (entry.Status == QuestEntryStatus.Completed
                    ? StatusComplete * (alpha * 0.75f)
                    : PrimaryBright * alpha);
            if (entry.IsNew) {
                float newBlink = MathF.Sin(pulseTimer * 4f) * 0.3f + 0.7f;
                titleColor = Color.Lerp(titleColor, AccentGold, newBlink * 0.4f);
            }

            //截断标题，并给右侧状态标签预留空间
            string statusText = GetEntryStatusText(entry.Status);
            float statusBadgeScale = 0.55f;
            int statusBadgeW = GetStatusBadgeWidth(statusText, statusBadgeScale);
            float statusBadgeX = entryRect.Right - statusBadgeW - 28f;
            string displayTitle = entry.Title ?? "";
            float maxEntryTitleW = Math.Max(40f, statusBadgeX - titleX - 8f);
            if (font.MeasureString(displayTitle).X * 0.90f > maxEntryTitleW) {
                while (displayTitle.Length > 3 && font.MeasureString(displayTitle + "...").X * 0.90f > maxEntryTitleW)
                    displayTitle = displayTitle[..^1];
                displayTitle += "...";
            }
            Utils.DrawBorderString(sb, displayTitle, new Vector2(titleX, titleY), titleColor, 0.90f);
            Rectangle statusBadgeRect = new((int)statusBadgeX, entryRect.Y + 8, statusBadgeW, 15);
            DrawHotwindStatusBadge(sb, statusBadgeRect, statusText, entry.Status,
                alpha, statusBadgeScale, entryIndex);

            //Tracked态火焰指示器
            if (entry.Status == QuestEntryStatus.Tracked) {
                float flameBlink = MathF.Sin(pulseTimer * 3f) * 0.4f + 0.6f;
                float titleW = font.MeasureString(displayTitle).X * 0.78f;
                Color flameColor = customStyle?.GetAccentColor(QuestEntryStatus.Tracked, alpha * flameBlink)
                    ?? AccentGold * (alpha * flameBlink);
                if (titleX + titleW + 18f < statusBadgeX) {
                    Utils.DrawBorderString(sb, "◆",
                        new Vector2(titleX + titleW + 6f, titleY + 1f),
                        flameColor, 0.65f);
                }
            }

            //摘要文本
            float summaryY = titleY + 20f;
            Color summaryColor = PrimaryMid * (alpha * 0.6f);
            string summary = (entry.Summary ?? "").Replace("\r", "").Replace("\n", " ").Trim();
            float maxSummaryW = entryRect.Width - 50f - iconOffset;
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

            //展开指示器
            if (!string.IsNullOrEmpty(entry.Summary)) {
                string expandIcon = entry.IsExpanded ? "▲" : "▼";
                float iconAlpha = isHovered ? 0.7f : 0.35f;
                float iconX = entryRect.Right - 18f;
                Utils.DrawBorderString(sb, expandIcon, new Vector2(iconX, titleY + 2f),
                    PrimaryMid * (alpha * iconAlpha), 0.6f);
            }

            //展开内容区域
            if (entry.ExpandProgress > 0.01f) {
                int baseH = GetEntryHeight();
                float expandAlpha = alpha * entry.ExpandProgress;
                float descY = entryRect.Y + baseH - 4f;

                //展开区域半透明背景
                int expandAreaH = entryRect.Height - baseH;
                if (expandAreaH > 0) {
                    int bgLeftPad = bgHandled ? 4 : 22;
                    Rectangle expandBg = new(entryRect.X + bgLeftPad, (int)descY - 2, entryRect.Width - bgLeftPad - 4, expandAreaH + 2);
                    FillRect(sb, expandBg, new Color(14, 8, 4) * (expandAlpha * 0.35f));
                }

                //分隔线（金属凹槽 + 暖色亮点扫掠）
                int sepW = (int)(entryRect.Width - titleX + entryRect.X - 14f);
                HLine(sb, (int)titleX, (int)descY, sepW, Color.Black * (expandAlpha * 0.5f));
                HLine(sb, (int)titleX, (int)descY + 1, sepW, PrimaryDim * (expandAlpha * 0.3f));
                float sepSweep = (int)titleX + flowTimer * 1.0f % 1f * sepW;
                for (int dx = -3; dx <= 3; dx++) {
                    int spx = (int)sepSweep + dx;
                    if (spx >= (int)titleX && spx < (int)titleX + sepW) {
                        float f = 1f - Math.Abs(dx) / 4f;
                        FillRect(sb, new Rectangle(spx, (int)descY, 1, 2), AccentGold * (expandAlpha * 0.4f * f * f));
                    }
                }
                descY += 6f;

                //自动换行描述文本
                string fullText = entry.Summary ?? "";
                float descScale = 0.70f;
                int wrapWidth = (int)((entryRect.Width - 40f) / descScale);
                Color descColor = new Color(220, 200, 170) * (expandAlpha * 0.75f);

                string[] paragraphs = fullText.Split('\n');
                foreach (string paragraph in paragraphs) {
                    string trimmedPara = paragraph.Trim();
                    if (string.IsNullOrEmpty(trimmedPara)) continue;
                    string[] wrapped = VaultUtils.WrapTextArray(trimmedPara, font, wrapWidth, 99, out _);
                    foreach (string wl in wrapped) {
                        if (string.IsNullOrEmpty(wl)) continue;
                        if (descY > entryRect.Bottom - 4f) break;
                        string trimmed = wl.TrimEnd('-', ' ');
                        Utils.DrawBorderString(sb, trimmed, new Vector2(titleX, descY), descColor, descScale);
                        descY += (int)(font.MeasureString(trimmed).Y * descScale) + 2;
                    }
                }
            }

            //进度条
            if (entry.Progress > 0f && entry.Status != QuestEntryStatus.Completed) {
                float barY = entry.ExpandProgress > 0.5f
                    ? entryRect.Bottom - 14f
                    : summaryY + 18f;
                int barW = Math.Min(120, entryRect.Width - 50);
                Rectangle barRect = new((int)titleX, (int)barY, barW, 5);
                DrawProgressBar(sb, barRect, entry.Progress,
                    new Color(12, 7, 4) * alpha,
                    PrimaryMid * alpha, AccentGold * alpha,
                    PrimaryDim * (alpha * 0.4f), pulseTimer);

                //流光
                float flow = flowTimer * 3f % 1f;
                int fillW = (int)(barW * MathHelper.Clamp(entry.Progress, 0f, 1f));
                int flowX = barRect.X + (int)(flow * fillW);
                if (fillW > 2 && flowX < barRect.X + fillW)
                    FillRect(sb, new Rectangle(flowX, barRect.Y, 2, barRect.Height), AccentGold * (alpha * 0.35f));

                if (entry.ProgressText != null) {
                    Utils.DrawBorderString(sb, entry.ProgressText,
                        new Vector2(barRect.Right + 6f, barRect.Y - 2f),
                        PrimaryMid * (alpha * 0.55f), 0.55f);
                }
            }

            //=== 前景特效 ===
            customStyle?.DrawEntryOverlay(sb, entryRect, entry, alpha);
        }

        //锻铁风状态铭牌：金属渐变、凹槽线和两侧铆钉，避免像外贴的普通按钮
        private void DrawHotwindStatusBadge(SpriteBatch sb, Rectangle badgeRect, string statusText,
            QuestEntryStatus status, float alpha, float scale, int entryIndex) {
            if (badgeRect.Width <= 0 || string.IsNullOrEmpty(statusText)) return;

            Color statusColor = GetStatusColor(status, 1f);
            float heat = MathF.Sin(pulseTimer * 2f + entryIndex * 0.35f) * 0.12f + 0.88f;
            Color top = Color.Lerp(new Color(34, 18, 8), statusColor, 0.22f);
            Color bottom = Color.Lerp(new Color(12, 7, 4), statusColor, 0.10f);
            for (int y = badgeRect.Y; y < badgeRect.Bottom; y++) {
                float t = (y - badgeRect.Y) / (float)Math.Max(1, badgeRect.Height - 1);
                HLine(sb, badgeRect.X, y, badgeRect.Width,
                    Color.Lerp(top, bottom, t) * (alpha * 0.78f));
            }

            HLine(sb, badgeRect.X + 2, badgeRect.Y, badgeRect.Width - 4,
                PrimaryBright * (alpha * 0.16f));
            HLine(sb, badgeRect.X + 1, badgeRect.Bottom - 1, badgeRect.Width - 2,
                Color.Black * (alpha * 0.35f));
            VLine(sb, badgeRect.X, badgeRect.Y + 2, badgeRect.Height - 4,
                statusColor * (alpha * 0.42f));
            VLine(sb, badgeRect.Right - 1, badgeRect.Y + 2, badgeRect.Height - 4,
                Color.Black * (alpha * 0.22f));

            Vector2 leftRivet = new(badgeRect.X + 4f, badgeRect.Y + badgeRect.Height / 2f);
            Vector2 rightRivet = new(badgeRect.Right - 4f, leftRivet.Y);
            sb.Draw(Px, leftRivet, null, AccentGold * (alpha * 0.50f * heat),
                0f, new Vector2(0.5f), 2.2f, SpriteEffects.None, 0f);
            sb.Draw(Px, rightRivet, null, AccentGold * (alpha * 0.35f * heat),
                0f, new Vector2(0.5f), 2.2f, SpriteEffects.None, 0f);

            Utils.DrawBorderString(sb, statusText,
                new Vector2(badgeRect.X + 9f, badgeRect.Y + 1f),
                statusColor * (alpha * (status == QuestEntryStatus.Active ? 0.74f : 0.95f)), scale);
        }

        //状态铆钉节点（金属圆钉风格）
        private void DrawStatusRivet(SpriteBatch sb, Vector2 center, QuestEntryStatus status, float alpha, int index) {
            float pulse = MathF.Sin(pulseTimer + index * 0.8f) * 0.2f + 0.8f;
            Color nodeColor = GetStatusColor(status, alpha * pulse);

            if (status == QuestEntryStatus.Completed) {
                //实心圆钉
                sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), nodeColor, 0f,
                    new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);
                //高光
                sb.Draw(Px, center + new Vector2(-1, -1), new Rectangle(0, 0, 1, 1),
                    Color.White * (alpha * 0.3f * pulse), 0f, new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
            }
            else if (status == QuestEntryStatus.Failed) {
                //交叉标记
                sb.Draw(Px, center, null, nodeColor, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(5f, 1.5f), SpriteEffects.None, 0f);
                sb.Draw(Px, center, null, nodeColor, -MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(5f, 1.5f), SpriteEffects.None, 0f);
            }
            else {
                //空心圆钉
                if (status == QuestEntryStatus.Tracked) {
                    float glowPulse = MathF.Sin(pulseTimer * 3f + index) * 0.3f + 0.3f;
                    sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), AccentGold * (alpha * glowPulse * 0.15f), 0f,
                        new Vector2(0.5f), 12f, SpriteEffects.None, 0f);
                }
                sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), nodeColor, 0f,
                    new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);
                sb.Draw(Px, center, new Rectangle(0, 0, 1, 1), BgDeep * (alpha * 0.9f), 0f,
                    new Vector2(0.5f), new Vector2(3f, 3f), SpriteEffects.None, 0f);
            }
        }

        public override void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            //金属铆钉虚线
            int dashLen = 5, gapLen = 5;
            float x = start.X;
            int dotIndex = 0;
            while (x < end.X) {
                float segEnd = Math.Min(x + dashLen, end.X);
                if (segEnd > x)
                    HLine(sb, (int)x, (int)start.Y, (int)(segEnd - x), PrimaryDim * (alpha * 0.15f));
                //每隔3段画一个铆钉点
                if (dotIndex % 3 == 1) {
                    float midX = x + dashLen / 2f;
                    sb.Draw(Px, new Vector2(midX, start.Y), new Rectangle(0, 0, 1, 1),
                        PrimaryMid * (alpha * 0.2f), 0f, new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
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
                QuestEntryStatus.Tracked => AccentGold * alpha,
                QuestEntryStatus.Suspended => StatusSuspended * alpha,
                QuestEntryStatus.Completed => StatusComplete * alpha,
                QuestEntryStatus.Failed => StatusFailed * alpha,
                _ => PrimaryDim * alpha,
            };
        }

        #endregion

        #region 粒子与特效

        public override void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha) {
            foreach (var p in sparkParticles) {
                Vector2 drawPos = new(
                    panelRect.X + p.Pos.X * panelRect.Width,
                    panelRect.Y + p.Pos.Y * panelRect.Height);
                if (drawPos.X < panelRect.X || drawPos.X > panelRect.Right ||
                    drawPos.Y < panelRect.Y || drawPos.Y > panelRect.Bottom) continue;

                float lifeRatio = p.Life / p.MaxLife;
                float fade = MathF.Sin(lifeRatio * MathHelper.Pi);
                Color pColor = AccentCopper * (fade * 0.45f * alpha);

                if (p.Type == 0) {
                    //火花
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor, 0f,
                        new Vector2(0.5f), new Vector2(p.Size * 1.8f, p.Size * 1.8f), SpriteEffects.None, 0f);
                }
                else if (p.Type == 1) {
                    //余烬（拉长条）
                    float rot = p.Vel.ToRotation();
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor, rot,
                        new Vector2(0.5f, 0.5f), new Vector2(p.Size * 3.5f, p.Size * 0.5f), SpriteEffects.None, 0f);
                }
                else {
                    //亮点
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor * 1.4f, 0f,
                        new Vector2(0.5f), p.Size, SpriteEffects.None, 0f);
                }
            }
        }

        public override void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha) {
            //暖色扫描线叠层
            Color scanC = new(30, 16, 6);
            for (int y = panelRect.Y; y < panelRect.Bottom; y += 3)
                HLine(sb, panelRect.X + 2, y, panelRect.Width - 4, scanC * (alpha * 0.04f));

            //铜色扫掠线
            float sweepY = panelRect.Y + shaderTime * 0.055f % 1f * panelRect.Height;
            Color sweepC = new(120, 55, 18);
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)sweepY + dy;
                if (py < panelRect.Y || py >= panelRect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 5f;
                HLine(sb, panelRect.X + 4, py, panelRect.Width - 8, sweepC * (alpha * 0.06f * f * f));
            }

            //暖色脉动叠层
            float flicker = MathF.Sin(globalTimer * 2f) * 0.5f + 0.5f;
            FillRect(sb, panelRect, new Color(50, 25, 10) * (alpha * 0.02f * flicker));
        }

        #endregion

        #region 样式切换按钮

        public override Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) =>
            new(panelRect.Right - 180, panelRect.Y + 6, 26, 26);

        public override void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetStyleSwitchButtonRect(panelRect);
            DrawSmallMetalButton(sb, btnRect, isHovered, alpha, DrawPageIcon);
        }

        //金属小按钮通用绘制
        private void DrawSmallMetalButton(SpriteBatch sb, Rectangle rect, bool hover, float alpha,
            Action<SpriteBatch, Vector2, float> drawIcon) {
            //投影
            Rectangle shadow = rect;
            shadow.Offset(1, 2);
            FillRect(sb, shadow, Color.Black * (0.35f * alpha));

            //金属渐变
            Color topC = hover ? new Color(130, 90, 50) : new Color(90, 60, 35);
            Color botC = hover ? new Color(80, 50, 25) : new Color(55, 35, 18);
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1f) / 4f;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                FillRect(sb, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    Color.Lerp(topC, botC, t) * alpha);
            }

            //顶部反光
            HLine(sb, rect.X + 2, rect.Y, rect.Width - 4, Color.White * (0.18f * alpha));

            //光照边缘
            Color edgeC = hover ? Color.White : new Color(210, 140, 70);
            HLine(sb, rect.X, rect.Y, rect.Width, edgeC * (0.5f * alpha));
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * (0.3f * alpha));
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * (0.25f * alpha));
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, Color.Black * (0.35f * alpha));

            drawIcon?.Invoke(sb, rect.Center.ToVector2(), alpha);
        }

        //页面图标（双页叠放）
        private void DrawPageIcon(SpriteBatch sb, Vector2 center, float alpha) {
            Color ic = new Color(230, 225, 210) * alpha;
            sb.Draw(Px, center + new Vector2(2, -2), new Rectangle(0, 0, 14, 18),
                ic * 0.4f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1), new Rectangle(0, 0, 14, 18),
                ic * 0.8f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, -4),
                new Rectangle(0, 0, 7, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 0),
                new Rectangle(0, 0, 7, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 4),
                new Rectangle(0, 0, 5, 1), Color.Black * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 工具方法

        //金属边框
        private static void DrawMetallicBorder(SpriteBatch sb, Rectangle rect, Color color) {
            HLine(sb, rect.X, rect.Y, rect.Width, color);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, color * 0.5f);
            VLine(sb, rect.X, rect.Y, rect.Height, color * 0.7f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * 0.2f);
        }

        #endregion
    }
}
