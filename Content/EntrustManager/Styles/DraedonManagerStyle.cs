using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.EntrustManager.Styles
{
    /// <summary>嘉登精密工程任务管理器样式</summary>
    internal class DraedonManagerStyle : BaseManagerStyle
    {
        #region 动画计时器

        private float shaderTime;
        private float dataFlowTimer;
        private float chevronPulse;
        private float headerGlowPhase;
        private float scanTimer;
        private const int EdgePad = 12;

        //数据流粒子（与QuestLogStyle统一结构）
        private struct DataParticle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Life;
            public float MaxLife;
            public float Size;
            public int Type; // 0=方块 1=短线 2=亮点
        }
        private readonly List<DataParticle> dataParticles = [];

        #endregion

        #region 色板

        private static readonly Color PrimaryBright = new(140, 210, 255);
        private static readonly Color PrimaryMid = new(60, 150, 220);
        private static readonly Color PrimaryDim = new(30, 80, 140);
        private static readonly Color AccentCyan = new(80, 255, 220);
        private static readonly Color AccentWarm = new(255, 200, 100);
        private static readonly Color BgDeep = new(4, 8, 20);
        private static readonly Color StatusComplete = new(60, 220, 140);
        private static readonly Color StatusFailed = new(220, 60, 70);
        private static readonly Color StatusSuspended = new(160, 140, 100);

        #endregion

        #region 生命周期

        public override void Update(Rectangle panelRect, float openProgress) {
            base.Update(panelRect, openProgress);

            shaderTime += 0.004f;
            if (shaderTime > 100f) shaderTime -= 100f;

            dataFlowTimer += 0.008f;
            if (dataFlowTimer > 1000f) dataFlowTimer -= 1000f;

            chevronPulse += 0.065f;
            if (chevronPulse > MathHelper.TwoPi) chevronPulse -= MathHelper.TwoPi;

            headerGlowPhase += 0.04f;
            if (headerGlowPhase > MathHelper.TwoPi) headerGlowPhase -= MathHelper.TwoPi;

            scanTimer += 0.04f;
            if (scanTimer > MathHelper.TwoPi) scanTimer -= MathHelper.TwoPi;

            //数据流粒子
            if (openProgress > 0.3f && Main.rand.NextBool(6)) {
                dataParticles.Add(new DataParticle {
                    Pos = new Vector2(Main.rand.NextFloat(0, 1f), Main.rand.NextFloat(0, 1f)),
                    Vel = new Vector2(Main.rand.NextFloat(-0.001f, 0.001f), Main.rand.NextFloat(-0.003f, -0.001f)),
                    Life = Main.rand.NextFloat(80f, 160f),
                    MaxLife = Main.rand.NextFloat(80f, 160f),
                    Size = Main.rand.NextFloat(1f, 3f),
                    Type = Main.rand.Next(3)
                });
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                var p = dataParticles[i];
                p.Life -= 1f;
                p.Pos += p.Vel;
                dataParticles[i] = p;
                if (p.Life <= 0) dataParticles.RemoveAt(i);
            }
        }

        public override void Reset() {
            base.Reset();
            shaderTime = 0f;
            dataFlowTimer = 0f;
            chevronPulse = 0f;
            headerGlowPhase = 0f;
            scanTimer = 0f;
            dataParticles.Clear();
        }

        #endregion

        #region 着色器面板背景

        //DraedonPanel 着色器底图，降级手绘
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            if (EffectLoader.DraedonPanel?.Value != null) {
                Effect effect = EffectLoader.DraedonPanel.Value;
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
            Color top = new(6, 12, 22);
            Color mid = new(4, 8, 16);
            Color bot = new(2, 5, 10);

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

            //CRT扫描线
            Color scanC = new(12, 25, 50);
            for (int y = rect.Y; y < rect.Bottom; y += 3)
                HLine(sb, rect.X + 2, y, rect.Width - 4, scanC * (alpha * 0.08f));

            //网格线
            Color gridC = new(18, 45, 80);
            int gridSpacing = 40;
            for (int gx = rect.X + gridSpacing; gx < rect.Right; gx += gridSpacing)
                VLine(sb, gx, rect.Y + 4, rect.Height - 8, gridC * (alpha * 0.06f));
            for (int gy = rect.Y + gridSpacing; gy < rect.Bottom; gy += gridSpacing)
                HLine(sb, rect.X + 4, gy, rect.Width - 8, gridC * (alpha * 0.06f));

            //暗角
            int vigW = 30;
            for (int v = 0; v < vigW; v += 3) {
                float fade = 1f - v / (float)vigW;
                fade *= fade;
                Color vc = Color.Black * (alpha * 0.2f * fade);
                FillRect(sb, new Rectangle(rect.X + v, rect.Y, 2, rect.Height), vc);
                FillRect(sb, new Rectangle(rect.Right - v - 2, rect.Y, 2, rect.Height), vc);
            }

            //扫掠线
            float sweepY = rect.Y + shaderTime * 0.06f % 1f * rect.Height;
            Color sweepC = new(30, 80, 150);
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)sweepY + dy;
                if (py < rect.Y || py >= rect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 5f;
                HLine(sb, rect.X + 2, py, rect.Width - 4, sweepC * (alpha * 0.1f * f * f));
            }
        }

        #endregion

        #region 面板背景

        public override void DrawPanelBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            //多层扩散阴影
            DrawShadowLayers(sb, rect, alpha, 10, 4, 5);
            //着色器驱动的面板底图
            DrawShaderPanel(sb, rect, alpha);
        }

        #endregion

        #region 面板边框

        public override void DrawPanelFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            //科技边框：顶部亮线+底部暗线+左侧强调+右侧淡线
            Color edgeC = PrimaryBright * (alpha * 0.8f);
            HLine(sb, rect.X, rect.Y, rect.Width, edgeC);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, PrimaryDim * (alpha * 0.5f));
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * 0.7f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, PrimaryDim * (alpha * 0.3f));

            //四角标点
            int cs = 5;
            Color cornerC = AccentCyan * (alpha * 0.9f);
            HLine(sb, rect.X, rect.Y, cs, cornerC);
            VLine(sb, rect.X, rect.Y, cs, cornerC);
            HLine(sb, rect.Right - cs, rect.Y, cs, cornerC);
            VLine(sb, rect.Right - 1, rect.Y, cs, cornerC);
            HLine(sb, rect.X, rect.Bottom - 1, cs, cornerC);
            VLine(sb, rect.X, rect.Bottom - cs, cs, cornerC);
            HLine(sb, rect.Right - cs, rect.Bottom - 1, cs, cornerC);
            VLine(sb, rect.Right - 1, rect.Bottom - cs, cs, cornerC);

            //角落电路节点装饰
            float nodePulse = MathF.Sin(pulseTimer) * 0.5f + 0.5f;
            DrawCircuitNode(sb, new Vector2(rect.X + 10, rect.Y + 10), nodePulse, alpha);
            DrawCircuitNode(sb, new Vector2(rect.Right - 10, rect.Y + 10), nodePulse, alpha);
            DrawCircuitNode(sb, new Vector2(rect.X + 10, rect.Bottom - 10), nodePulse * 0.6f, alpha);
            DrawCircuitNode(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), nodePulse * 0.6f, alpha);

            //角落走线装饰
            DrawCircuitTrace(sb, new Vector2(rect.X + 10, rect.Y + 10), true, true, alpha);
            DrawCircuitTrace(sb, new Vector2(rect.Right - 10, rect.Y + 10), false, true, alpha);
        }

        //电路节点装饰
        private void DrawCircuitNode(SpriteBatch sb, Vector2 pos, float pulse, float alpha) {
            Color c = PrimaryBright;
            //外圈辉光
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), c * (0.15f * pulse * alpha), 0f,
                new Vector2(0.5f), 10f, SpriteEffects.None, 0f);
            //十字电路线
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), c * (0.7f * pulse * alpha), 0f,
                new Vector2(0.5f), new Vector2(8f, 1f), SpriteEffects.None, 0f);
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), c * (0.6f * pulse * alpha), MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(8f, 1f), SpriteEffects.None, 0f);
            //菱形核心
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), AccentCyan * (0.9f * pulse * alpha), MathHelper.PiOver4,
                new Vector2(0.5f), 3f, SpriteEffects.None, 0f);
            //高光点
            sb.Draw(Px, pos, new Rectangle(0, 0, 1, 1), Color.White * (0.3f * pulse * alpha), 0f,
                new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
        }

        //角落电路走线
        private void DrawCircuitTrace(SpriteBatch sb, Vector2 corner, bool goRight, bool goDown, float alpha) {
            int dirX = goRight ? 1 : -1;
            int dirY = goDown ? 1 : -1;
            Color traceC = PrimaryMid * (alpha * 0.2f);
            //水平走线
            FillRect(sb, new Rectangle((int)corner.X, (int)corner.Y, 22 * dirX, 1), traceC);
            //转角
            Vector2 turn = corner + new Vector2(22 * dirX, 0);
            FillRect(sb, new Rectangle((int)turn.X, (int)turn.Y, 1, 14 * dirY), traceC);
            //末端方块
            Vector2 end = turn + new Vector2(0, 14 * dirY);
            sb.Draw(Px, end, new Rectangle(0, 0, 1, 1), PrimaryBright * (alpha * 0.35f), 0f,
                new Vector2(0.5f), 2f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 标题栏

        public override void DrawHeader(SpriteBatch sb, Rectangle headerRect, string title, float alpha) {
            //标题栏深色背景
            FillRect(sb, headerRect, new Color(3, 6, 16) * (alpha * 0.7f));

            //雷达扫描环装饰（标题左侧）
            Vector2 radarCenter = new(headerRect.X + 22f, headerRect.Y + headerRect.Height / 2f);
            float scanRot = scanTimer;
            for (int i = 0; i < 4; i++) {
                float r = scanRot + i * MathHelper.PiOver2;
                Vector2 tickEnd = radarCenter + new Vector2(0, -10).RotatedBy(r);
                sb.Draw(Px, tickEnd, new Rectangle(0, 0, 1, 3), PrimaryMid * (alpha * 0.4f),
                    (float)r, new Vector2(0.5f, 1.5f), 1f, SpriteEffects.None, 0f);
            }
            //雷达中心亮点
            FillRect(sb, new Rectangle((int)(radarCenter.X - 1), (int)(radarCenter.Y - 1), 3, 3),
                AccentCyan * (alpha * 0.5f));

            //标题文字
            var font = FontAssets.MouseText.Value;
            float headerBlink = MathF.Sin(headerGlowPhase) * 0.12f + 0.88f;
            Vector2 titlePos = new(headerRect.X + 40f, headerRect.Y + (headerRect.Height - 18f) / 2f);
            float maxHeaderTitleW = headerRect.Width - 110f;
            if (font.MeasureString(title).X * 1.0f > maxHeaderTitleW) {
                while (title.Length > 3 && font.MeasureString(title + "...").X * 1.0f > maxHeaderTitleW)
                    title = title[..^1];
                title += "...";
            }
            Utils.DrawBorderString(sb, title, titlePos, PrimaryBright * (alpha * headerBlink), 1.0f);

            //右侧状态标签
            float tagBlink = MathF.Sin(headerGlowPhase * 1.6f) * 0.35f + 0.65f;
            string tag = QuestManagerUI.HeaderStatusTag.Value;
            float tagW = font.MeasureString(tag).X * 0.6f;
            Utils.DrawBorderString(sb, tag,
                new Vector2(headerRect.Right - tagW - 14f, headerRect.Y + (headerRect.Height - 14f) / 2f),
                AccentCyan * (alpha * 0.55f * tagBlink), 0.6f);

            //底部分隔线（双线凹槽 + 扫掠亮点）
            int lineW = headerRect.Width - 16;
            int lineX = headerRect.X + 8;
            int lineY = headerRect.Bottom - 2;
            HLine(sb, lineX, lineY, lineW, new Color(0, 0, 0) * (alpha * 0.8f));
            HLine(sb, lineX, lineY + 1, lineW, PrimaryMid * (alpha * 0.5f));
            //扫掠亮点
            float sweepX = lineX + dataFlowTimer * 0.8f % 1f * lineW;
            for (int dx = -4; dx <= 4; dx++) {
                int px = (int)sweepX + dx;
                if (px >= lineX && px < lineX + lineW) {
                    float f = 1f - Math.Abs(dx) / 5f;
                    FillRect(sb, new Rectangle(px, lineY, 1, 2), AccentCyan * (alpha * 0.6f * f * f));
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

                //底色 + 扫描线纹理
                Color bgC = selected ? PrimaryDim * (alpha * 0.35f) : new Color(6, 12, 28) * (alpha * 0.25f);
                FillRect(sb, tab, bgC);
                for (int sy = tab.Y; sy < tab.Bottom; sy += 3)
                    HLine(sb, tab.X + 1, sy, tab.Width - 2, PrimaryMid * (alpha * 0.04f));

                if (selected) {
                    //选中态底部高亮线
                    HLine(sb, tab.X, tab.Bottom - 1, tab.Width, 2, AccentCyan * (alpha * 0.85f));
                    //顶部亮边
                    HLine(sb, tab.X, tab.Y, tab.Width, PrimaryBright * (alpha * 0.2f));
                }

                //科技角标
                if (selected) {
                    int cs2 = 3;
                    Color csC = AccentCyan * (alpha * 0.7f);
                    HLine(sb, tab.X, tab.Y, cs2, csC);
                    VLine(sb, tab.X, tab.Y, cs2, csC);
                    HLine(sb, tab.Right - cs2, tab.Bottom - 1, cs2, csC);
                    VLine(sb, tab.Right - 1, tab.Bottom - cs2, cs2, csC);
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
            FillRect(sb, trackRect, new Color(5, 8, 14) * (alpha * 0.5f));
            VLine(sb, trackRect.X, trackRect.Y, trackRect.Height, PrimaryDim * (alpha * 0.25f));

            //滑块
            float clampedView = MathHelper.Clamp(viewRatio, 0.1f, 1f);
            int thumbH = Math.Max(20, (int)(trackRect.Height * clampedView));
            int thumbY = trackRect.Y + (int)((trackRect.Height - thumbH) * MathHelper.Clamp(scrollRatio, 0f, 1f));
            Rectangle thumb = new(trackRect.X + 1, thumbY, trackRect.Width - 2, thumbH);

            //渐变填充
            float thumbPulse = MathF.Sin(pulseTimer * 1.5f) * 0.15f + 0.55f;
            for (int ty = thumb.Y; ty < thumb.Bottom; ty++) {
                float t = (ty - thumb.Y) / (float)thumb.Height;
                Color c = Color.Lerp(PrimaryMid * 0.8f, PrimaryDim * 0.4f, t);
                HLine(sb, thumb.X, ty, thumb.Width, c * (alpha * thumbPulse));
            }

            //薄顶部高光
            HLine(sb, thumb.X, thumb.Y, thumb.Width, Color.White * (alpha * 0.15f));

            //流光
            float flow = dataFlowTimer * 4f % 1f;
            int flowY = thumb.Y + (int)(flow * thumb.Height);
            if (flowY >= thumb.Y && flowY < thumb.Bottom)
                HLine(sb, thumb.X, flowY, thumb.Width, Color.White * (alpha * 0.3f));

            //科技边框
            DrawThinTechBorder(sb, thumb, PrimaryMid * (alpha * 0.5f));
        }

        #endregion

        #region 底部状态栏

        public override void DrawFooter(SpriteBatch sb, Rectangle footerRect, int totalQuests,
            int activeQuests, float alpha) {
            //背景
            FillRect(sb, footerRect, new Color(3, 6, 16) * (alpha * 0.65f));

            //顶部分隔（双线凹槽）
            HLine(sb, footerRect.X + 8, footerRect.Y, footerRect.Width - 16, new Color(0, 0, 0) * (alpha * 0.6f));
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
                    //选中态科技边框
                    DrawThinTechBorder(sb, entryRect, AccentCyan * (alpha * 0.5f));
                }
                else if (isHovered) {
                    FillRect(sb, entryRect, PrimaryDim * (alpha * 0.1f));
                    DrawThinTechBorder(sb, entryRect, PrimaryMid * (alpha * 0.25f));
                }

                //状态指示条（左侧3px竖条）
                Color statusBarColor = GetStatusColor(entry.Status, alpha);
                VLine(sb, entryRect.X + 2, entryRect.Y + 4, entryRect.Height - 8, 3, statusBarColor);

                //状态菱形节点
                int nodeX = entryRect.X + 16;
                int nodeY = entryRect.Y + 14;
                DrawStatusNode(sb, new Vector2(nodeX, nodeY), entry.Status, alpha, entryIndex);
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
                float newBlink = MathF.Sin(chevronPulse * 2f) * 0.3f + 0.7f;
                titleColor = Color.Lerp(titleColor, AccentWarm, newBlink * 0.4f);
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
            DrawDraedonStatusBadge(sb, statusBadgeRect, statusText, entry.Status,
                alpha, statusBadgeScale, entryIndex);

            //Tracked态雪佛龙指示器
            if (entry.Status == QuestEntryStatus.Tracked) {
                float chevBlink = MathF.Sin(chevronPulse * 1.5f) * 0.4f + 0.6f;
                float titleW = font.MeasureString(displayTitle).X * 0.78f;
                Color chevColor = customStyle?.GetAccentColor(QuestEntryStatus.Tracked, alpha * chevBlink)
                    ?? AccentCyan * (alpha * chevBlink);
                if (titleX + titleW + 18f < statusBadgeX) {
                    Utils.DrawBorderString(sb, "››",
                        new Vector2(titleX + titleW + 6f, titleY + 1f),
                        chevColor, 0.7f);
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
                    FillRect(sb, expandBg, new Color(4, 8, 18) * (expandAlpha * 0.35f));
                }

                //分隔线（双线凹槽 + 扫掠亮点）
                int sepW = (int)(entryRect.Width - titleX + entryRect.X - 14f);
                HLine(sb, (int)titleX, (int)descY, sepW, new Color(0, 0, 0) * (expandAlpha * 0.6f));
                HLine(sb, (int)titleX, (int)descY + 1, sepW, PrimaryDim * (expandAlpha * 0.35f));
                float sepSweep = (int)titleX + dataFlowTimer * 1.2f % 1f * sepW;
                for (int dx = -3; dx <= 3; dx++) {
                    int spx = (int)sepSweep + dx;
                    if (spx >= (int)titleX && spx < (int)titleX + sepW) {
                        float f = 1f - Math.Abs(dx) / 4f;
                        FillRect(sb, new Rectangle(spx, (int)descY, 1, 2), AccentCyan * (expandAlpha * 0.5f * f * f));
                    }
                }
                descY += 6f;

                //自动换行描述文本
                string fullText = entry.Summary ?? "";
                float descScale = 0.70f;
                int wrapWidth = (int)((entryRect.Width - 40f) / descScale);
                Color descColor = new Color(170, 200, 220) * (expandAlpha * 0.75f);

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
                    new Color(5, 8, 14) * alpha,
                    PrimaryMid * alpha, AccentCyan * alpha,
                    PrimaryDim * (alpha * 0.4f), pulseTimer);

                //流光
                float flow = dataFlowTimer * 4f % 1f;
                int fillW = (int)(barW * MathHelper.Clamp(entry.Progress, 0f, 1f));
                int flowX = barRect.X + (int)(flow * fillW);
                if (fillW > 2 && flowX < barRect.X + fillW)
                    FillRect(sb, new Rectangle(flowX, barRect.Y, 2, barRect.Height), Color.White * (alpha * 0.4f));

                if (entry.ProgressText != null) {
                    Utils.DrawBorderString(sb, entry.ProgressText,
                        new Vector2(barRect.Right + 6f, barRect.Y - 2f),
                        PrimaryMid * (alpha * 0.55f), 0.55f);
                }
            }

            //=== 前景特效 ===
            customStyle?.DrawEntryOverlay(sb, entryRect, entry, alpha);
        }

        //科技风状态铭牌：微弱全息底、角标和数据节点，贴合嘉登面板语言
        private void DrawDraedonStatusBadge(SpriteBatch sb, Rectangle badgeRect, string statusText,
            QuestEntryStatus status, float alpha, float scale, int entryIndex) {
            if (badgeRect.Width <= 0 || string.IsNullOrEmpty(statusText)) return;

            Color statusColor = GetStatusColor(status, 1f);
            float pulse = MathF.Sin(chevronPulse + entryIndex * 0.45f) * 0.18f + 0.82f;
            FillRect(sb, badgeRect, BgDeep * (alpha * 0.55f));
            DrawGradientHLine(sb, badgeRect.X + 1, badgeRect.Y + 1, badgeRect.Width - 2,
                statusColor * (alpha * 0.30f), Color.Transparent, 10);
            HLine(sb, badgeRect.X + 1, badgeRect.Bottom - 2, badgeRect.Width - 2,
                PrimaryDim * (alpha * 0.35f));

            Color cornerColor = statusColor * (alpha * 0.75f * pulse);
            HLine(sb, badgeRect.X, badgeRect.Y, 5, cornerColor);
            VLine(sb, badgeRect.X, badgeRect.Y, 5, cornerColor);
            HLine(sb, badgeRect.Right - 5, badgeRect.Bottom - 1, 5, cornerColor);
            VLine(sb, badgeRect.Right - 1, badgeRect.Bottom - 5, 5, cornerColor);

            Vector2 node = new(badgeRect.X + 4f, badgeRect.Y + badgeRect.Height / 2f);
            sb.Draw(Px, node, null, statusColor * (alpha * 0.75f * pulse),
                MathHelper.PiOver4, new Vector2(0.5f), 2.4f, SpriteEffects.None, 0f);
            Utils.DrawBorderString(sb, statusText,
                new Vector2(badgeRect.X + 10f, badgeRect.Y + 1f),
                statusColor * (alpha * (status == QuestEntryStatus.Active ? 0.72f : 0.95f)), scale);
        }

        //菱形状态节点（多层全息风格，与QuestLogStyle的节点统一）
        private void DrawStatusNode(SpriteBatch sb, Vector2 center, QuestEntryStatus status, float alpha, int index) {
            float nodeSize = 4f;
            float pulse = MathF.Sin(pulseTimer + index * 0.8f) * 0.2f + 0.8f;
            Color nodeColor = GetStatusColor(status, alpha * pulse);

            if (status == QuestEntryStatus.Completed) {
                //实心菱形
                sb.Draw(Px, center, null, nodeColor, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(nodeSize * 1.4f), SpriteEffects.None, 0f);
                //高光点
                sb.Draw(Px, center, null, Color.White * (alpha * 0.3f * pulse), 0f,
                    new Vector2(0.5f), 1.5f, SpriteEffects.None, 0f);
            }
            else if (status == QuestEntryStatus.Failed) {
                sb.Draw(Px, center, null, nodeColor, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(nodeSize * 1.2f, nodeSize * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(Px, center, null, nodeColor, -MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(nodeSize * 1.2f, nodeSize * 0.3f), SpriteEffects.None, 0f);
            }
            else {
                //空心菱形（多层全息辉光）
                if (status == QuestEntryStatus.Tracked) {
                    float glowPulse = MathF.Sin(chevronPulse + index) * 0.3f + 0.3f;
                    sb.Draw(Px, center, null, AccentCyan * (alpha * glowPulse * 0.15f), MathHelper.PiOver4,
                        new Vector2(0.5f), nodeSize * 3f, SpriteEffects.None, 0f);
                }
                sb.Draw(Px, center, null, nodeColor, MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(nodeSize * 1.4f), SpriteEffects.None, 0f);
                sb.Draw(Px, center, null, BgDeep * (alpha * 0.9f), MathHelper.PiOver4, new Vector2(0.5f),
                    new Vector2(nodeSize * 0.8f), SpriteEffects.None, 0f);
            }
        }

        public override void DrawEntrySeparator(SpriteBatch sb, Vector2 start, Vector2 end, float alpha) {
            //方块点阵虚线
            int dashLen = 3, gapLen = 4;
            float x = start.X;
            while (x < end.X) {
                float segEnd = Math.Min(x + dashLen, end.X);
                if (segEnd > x) {
                    HLine(sb, (int)x, (int)start.Y, (int)(segEnd - x), PrimaryDim * (alpha * 0.15f));
                }
                x += dashLen + gapLen;
            }
        }

        #endregion

        #region 颜色

        public override Color GetShadowColor(float alpha) => Color.Black * (alpha * 0.45f);

        public override Color GetHeaderTextColor(float alpha) => PrimaryBright * alpha;

        public override Color GetStatusColor(QuestEntryStatus status, float alpha) {
            return status switch {
                QuestEntryStatus.Active => PrimaryMid * alpha,
                QuestEntryStatus.Tracked => AccentCyan * alpha,
                QuestEntryStatus.Suspended => StatusSuspended * alpha,
                QuestEntryStatus.Completed => StatusComplete * alpha,
                QuestEntryStatus.Failed => StatusFailed * alpha,
                _ => PrimaryDim * alpha,
            };
        }

        #endregion

        #region 粒子与特效

        public override void DrawParticles(SpriteBatch sb, Rectangle panelRect, float alpha) {
            Color accentColor = PrimaryBright;
            foreach (var p in dataParticles) {
                Vector2 drawPos = new(
                    panelRect.X + p.Pos.X * panelRect.Width,
                    panelRect.Y + p.Pos.Y * panelRect.Height);
                if (drawPos.X < panelRect.X || drawPos.X > panelRect.Right ||
                    drawPos.Y < panelRect.Y || drawPos.Y > panelRect.Bottom) continue;

                float lifeRatio = p.Life / p.MaxLife;
                float fade = MathF.Sin(lifeRatio * MathHelper.Pi);
                Color pColor = accentColor * (fade * 0.5f * alpha);

                if (p.Type == 0) {
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor, 0f,
                        new Vector2(0.5f), new Vector2(p.Size * 2f, p.Size * 2f), SpriteEffects.None, 0f);
                }
                else if (p.Type == 1) {
                    float rot = p.Vel.ToRotation();
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor, rot,
                        new Vector2(0.5f, 0.5f), new Vector2(p.Size * 4f, p.Size * 0.5f), SpriteEffects.None, 0f);
                }
                else {
                    sb.Draw(Px, drawPos, new Rectangle(0, 0, 1, 1), pColor * 1.5f, 0f,
                        new Vector2(0.5f), p.Size, SpriteEffects.None, 0f);
                }
            }
        }

        public override void DrawOverlayEffects(SpriteBatch sb, Rectangle panelRect, float alpha) {
            //CRT扫描线叠层
            Color scanC = new(12, 25, 50);
            for (int y = panelRect.Y; y < panelRect.Bottom; y += 3)
                HLine(sb, panelRect.X + 2, y, panelRect.Width - 4, scanC * (alpha * 0.05f));

            //扫掠线
            float sweepY = panelRect.Y + shaderTime * 0.06f % 1f * panelRect.Height;
            Color sweepC = new(30, 80, 150);
            for (int dy = -4; dy <= 4; dy++) {
                int py = (int)sweepY + dy;
                if (py < panelRect.Y || py >= panelRect.Bottom) continue;
                float f = 1f - Math.Abs(dy) / 5f;
                HLine(sb, panelRect.X + 4, py, panelRect.Width - 8, sweepC * (alpha * 0.08f * f * f));
            }

            //全息闪烁叠层
            float flicker = MathF.Sin(globalTimer * 2.4f) * 0.5f + 0.5f;
            FillRect(sb, panelRect, new Color(20, 40, 60) * (alpha * 0.03f * flicker));
        }

        #endregion

        #region 工具方法

        //细线科技边框
        private static void DrawThinTechBorder(SpriteBatch sb, Rectangle rect, Color color) {
            HLine(sb, rect.X, rect.Y, rect.Width, color);
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, color * 0.6f);
            VLine(sb, rect.X, rect.Y, rect.Height, color * 0.8f);
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, color * 0.5f);
        }

        #endregion

        #region 样式切换按钮

        public override Rectangle GetStyleSwitchButtonRect(Rectangle panelRect) =>
            new(panelRect.Right - 180, panelRect.Y + 6, 26, 26);

        public override void DrawStyleSwitchButton(SpriteBatch sb, Rectangle panelRect, bool isHovered, float alpha) {
            Rectangle btnRect = GetStyleSwitchButtonRect(panelRect);
            DrawSmallTechButton(sb, btnRect, isHovered, alpha, DrawPageIcon);
        }

        //科技小按钮通用绘制
        private void DrawSmallTechButton(SpriteBatch sb, Rectangle rect, bool hover, float alpha,
            Action<SpriteBatch, Vector2, float> drawIcon) {
            //投影
            Rectangle shadow = rect;
            shadow.Offset(1, 2);
            FillRect(sb, shadow, Color.Black * (0.35f * alpha));

            //科技渐变
            Color topC = hover ? new Color(40, 80, 140) : new Color(20, 45, 80);
            Color botC = hover ? new Color(20, 45, 80) : new Color(10, 22, 45);
            for (int i = 0; i < 4; i++) {
                float t = i / 4f;
                float t2 = (i + 1f) / 4f;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                FillRect(sb, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    Color.Lerp(topC, botC, t) * alpha);
            }

            //顶部反光
            HLine(sb, rect.X + 2, rect.Y, rect.Width - 4, Color.White * (0.12f * alpha));

            //光照边缘
            Color edgeC = hover ? Color.White : PrimaryBright;
            HLine(sb, rect.X, rect.Y, rect.Width, edgeC * (0.5f * alpha));
            VLine(sb, rect.X, rect.Y, rect.Height, edgeC * (0.3f * alpha));
            VLine(sb, rect.Right - 1, rect.Y, rect.Height, Color.Black * (0.25f * alpha));
            HLine(sb, rect.X, rect.Bottom - 1, rect.Width, Color.Black * (0.35f * alpha));

            //角标
            if (hover) {
                int cs = 3;
                Color csC = AccentCyan * (alpha * 0.6f);
                HLine(sb, rect.X, rect.Y, cs, csC);
                VLine(sb, rect.X, rect.Y, cs, csC);
                HLine(sb, rect.Right - cs, rect.Bottom - 1, cs, csC);
                VLine(sb, rect.Right - 1, rect.Bottom - cs, cs, csC);
            }

            drawIcon?.Invoke(sb, rect.Center.ToVector2(), alpha);
        }

        //页面翻页图标
        private static void DrawPageIcon(SpriteBatch sb, Vector2 center, float alpha) {
            Color ic = new Color(180, 220, 255) * alpha;
            sb.Draw(Px, center + new Vector2(2, -2), new Rectangle(0, 0, 14, 18),
                ic * 0.35f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1), new Rectangle(0, 0, 14, 18),
                ic * 0.75f, 0f, new Vector2(7, 9), 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, -4),
                new Rectangle(0, 0, 7, 1), new Color(80, 200, 255) * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 0),
                new Rectangle(0, 0, 7, 1), new Color(80, 200, 255) * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(-1, 1) + new Vector2(-3, 4),
                new Rectangle(0, 0, 5, 1), new Color(80, 200, 255) * (0.4f * alpha), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        #endregion
    }
}
