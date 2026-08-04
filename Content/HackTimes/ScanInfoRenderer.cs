using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>左侧档案面板，头像 + 威胁刻度 + 逐行解码</summary>
    internal class ScanInfoRenderer
    {
        #region 状态字段

        private IHackTarget lastScanTarget;
        private int currentDataRowCount;
        private float scanProgress;//0~1
        private float revealTimer;//扫描完成后秒
        private int revealedRows;
        private float typewriterChar;
        private float timer;
        private float flyInProgress;//0~1
        private float glitchIntensity;

        #endregion

        #region 布局参数

        private const float LeftMargin = 36f;
        private const float PanelWidth = 340f;
        private const float RowHeight = 23f;
        private const float TabRowHeight = 22f;
        private const float TitleHeight = 30f;
        private const float SepHeight = 9f;
        private const float StatusHeight = 26f;
        private const float TopPad = 10f;
        private const float BottomPad = 10f;
        //头像格边长
        private const float PortraitSize = 86f;
        //头像格与右侧行区间距
        private const float PortraitGap = 12f;
        //威胁刻度行高
        private const float PipsRowHeight = 20f;
        //扫描时长(帧)
        private const float ScanDuration = 30f;
        //每行揭示间隔(秒)
        private const float RowRevealInterval = 0.13f;
        //打字机速度(字符/帧)
        private const float TypewriterSpeed = 2.5f;
        //数据行数组容量
        private const int MaxDataRowCount = 10;
        //MouseText 中文不低于 0.5
        private const float FontTitle = 0.86f;
        private const float FontRow = 0.72f;
        private const float FontLabel = 0.62f;
        private const float FontStatus = 0.64f;
        private const float FontMicro = 0.50f;

        #endregion

        //缓存的扫描数据
        private readonly string[] rowLabels = new string[MaxDataRowCount];
        private readonly string[] rowValues = new string[MaxDataRowCount];
        private readonly Color[] rowColors = new Color[MaxDataRowCount];
        private string statusText = "";
        private Color statusColor;

        #region 更新

        public void Update() {
            timer += 0.016f;

            IHackTarget currentTarget = HackTime.CurrentScanTarget;

            //目标切换时重置扫描
            if (currentTarget != lastScanTarget) {
                lastScanTarget = currentTarget;
                if (currentTarget != null) {
                    currentDataRowCount = Math.Min(currentTarget.ScanRowCount, MaxDataRowCount);
                    StartScan();
                }
                else {
                    scanProgress = 0f;
                    revealTimer = 0f;
                    revealedRows = 0;
                    typewriterChar = 0f;
                    currentDataRowCount = 0;
                }
            }

            if (currentTarget == null) {
                flyInProgress = MathHelper.Lerp(flyInProgress, 0f, 0.12f);
                return;
            }

            //飞入动画
            flyInProgress = MathHelper.Lerp(flyInProgress, 1f, 0.08f);
            if (flyInProgress > 0.995f) flyInProgress = 1f;

            //扫描阶段
            if (scanProgress < 1f) {
                scanProgress += 1f / ScanDuration;
                if (scanProgress >= 1f) {
                    scanProgress = 1f;
                    revealTimer = 0f;
                    revealedRows = 0;
                    typewriterChar = 0f;
                    glitchIntensity = 1f;
                    //IScannable 构建扫描数据
                    currentTarget?.BuildScanData(rowLabels, rowValues, rowColors);
                    statusText = HackTime.AnalysisComplete.Value;
                    statusColor = HackTheme.Accent;
                }
                return;
            }

            //数据行逐行揭示
            revealTimer += 0.016f;
            int targetRows = Math.Min((int)(revealTimer / RowRevealInterval) + 1, currentDataRowCount);
            if (revealedRows < targetRows) {
                revealedRows = targetRows;
                typewriterChar = 0f;
                glitchIntensity = 0.6f;
            }

            //打字机推进
            if (revealedRows > 0 && revealedRows <= currentDataRowCount) {
                string val = rowValues[revealedRows - 1] ?? "";
                typewriterChar = Math.Min(typewriterChar + TypewriterSpeed, val.Length);
            }

            //故障衰减
            glitchIntensity = MathHelper.Lerp(glitchIntensity, 0f, 0.08f);
        }

        private void StartScan() {
            scanProgress = 0f;
            revealTimer = 0f;
            revealedRows = 0;
            typewriterChar = 0f;
            glitchIntensity = 0.3f;
            statusText = HackTime.Scanning.Value;
            statusColor = HackTheme.Uploading;
        }

        #endregion

        #region 主绘制

        public void Draw(SpriteBatch sb) {
            if (lastScanTarget == null && flyInProgress < 0.01f) return;

            Texture2D px = HackTheme.Pixel;
            if (px == null) return;

            float alpha = HackTime.Intensity * flyInProgress;
            if (alpha < 0.01f) return;

            //头像右侧最多行数
            int sideRows = Math.Min(currentDataRowCount, 3);
            int belowRows = currentDataRowCount - sideRows;
            float portraitBlockH = PortraitSize + 4f + PipsRowHeight;
            float sideBlockH = Math.Max(portraitBlockH, sideRows * RowHeight);
            float panelH = TopPad + TabRowHeight + TitleHeight + SepHeight
                + sideBlockH + (belowRows > 0 ? 4f + belowRows * RowHeight : 0f)
                + SepHeight + StatusHeight + BottomPad;

            //左侧垂直居中
            float baseX = LeftMargin;
            float panelTop = (Main.screenHeight - panelH) * 0.5f;

            //飞入偏移（自左）
            float flyOffset = (1f - HackTheme.EaseOutCubic(flyInProgress)) * -300f;
            baseX += flyOffset;

            //故障抖动
            float shakeX = glitchIntensity * MathF.Sin(timer * 45f) * 3f;
            float shakeY = glitchIntensity * MathF.Cos(timer * 38f) * 1.5f;
            baseX += shakeX;
            panelTop += shakeY;

            Rectangle panelRect = new((int)baseX, (int)panelTop, (int)PanelWidth, (int)panelH);

            DrawPanelBackground(sb, px, panelRect, alpha);

            float curY = panelTop + TopPad;
            float textX = baseX + 14f;

            //标签页行
            DrawTabs(sb, textX, curY, alpha, panelRect);
            curY += TabRowHeight;

            //标题（目标名，打字机）
            string title = lastScanTarget?.LockFrameTitle ?? "";
            int titleChars = scanProgress < 1f
                ? (int)(title.Length * Math.Min(scanProgress * 2.5f, 1f))
                : title.Length;
            titleChars = Math.Clamp(titleChars, 0, title.Length);
            string visibleTitle = title[..titleChars];
            Utils.DrawBorderString(sb, visibleTitle, new Vector2(textX, curY),
                HackTheme.TextBright * (alpha * 0.95f), FontTitle);
            //光标
            if (titleChars < title.Length && (int)(timer * 8f) % 2 == 0) {
                float cursorX = textX + FontAssets.MouseText.Value.MeasureString(visibleTitle).X * FontTitle + 2;
                Utils.DrawBorderString(sb, "█", new Vector2(cursorX, curY),
                    HackTheme.Accent * (alpha * 0.55f), FontTitle);
            }
            curY += TitleHeight;

            //分隔虚线
            HackTheme.DrawDashedLine(sb, new Vector2(baseX + 10, curY), new Vector2(baseX + PanelWidth - 10, curY),
                1f, HackTheme.Border * (alpha * 0.5f), 5f, 4f);
            curY += SepHeight;

            //扫描阶段
            if (scanProgress < 1f) {
                DrawScanPhase(sb, px, baseX, curY, alpha);
                DrawScanLineOverlay(sb, px, panelRect, alpha);
                DrawOuterGlow(sb, panelRect, alpha);
                return;
            }

            //头像格 + 威胁刻度
            Rectangle portraitRect = new((int)textX, (int)curY, (int)PortraitSize, (int)PortraitSize);
            DrawPortrait(sb, px, portraitRect, alpha);
            DrawThreatPips(sb, new Vector2(textX, curY + PortraitSize + 6f), alpha);

            //头像右侧数据行
            int sideCount = Math.Min(revealedRows, sideRows);
            float sideX = textX + PortraitSize + PortraitGap;
            for (int i = 0; i < sideCount; i++) {
                DrawDataRow(sb, px, sideX, curY + i * RowHeight, i, alpha,
                    PanelWidth - PortraitSize - PortraitGap - 28f);
            }
            curY += sideBlockH;

            //下方整宽数据行
            if (belowRows > 0) {
                curY += 4f;
                for (int i = sideRows; i < revealedRows && i < currentDataRowCount; i++) {
                    DrawDataRow(sb, px, textX, curY + (i - sideRows) * RowHeight, i, alpha,
                        PanelWidth - 28f);
                }
                curY += belowRows * RowHeight;
            }

            //底部分隔与状态
            if (revealedRows >= currentDataRowCount) {
                HackTheme.DrawDashedLine(sb, new Vector2(baseX + 10, curY), new Vector2(baseX + PanelWidth - 10, curY),
                    1f, HackTheme.Border * (alpha * 0.5f), 5f, 4f);
                curY += SepHeight;

                float statusPulse = MathF.Sin(timer * 3f) * 0.12f + 0.88f;
                Utils.DrawBorderString(sb, statusText, new Vector2((int)textX, (int)curY),
                    Color.Lerp(statusColor, Color.White, 0.15f) * (alpha * statusPulse), FontStatus);

                string hexTag = $"0x{(int)(timer * 50) % 0xFFFF:X4}";
                HackTheme.DrawRawText(sb, hexTag, new Vector2(baseX + PanelWidth - 78, curY + 3),
                    HackTheme.Accent * (alpha * 0.5f), FontMicro);
            }

            DrawScanLineOverlay(sb, px, panelRect, alpha);
            DrawOuterGlow(sb, panelRect, alpha);
        }

        #endregion

        #region 面板背景与框架

        private void DrawPanelBackground(SpriteBatch sb, Texture2D px, Rectangle panelRect, float alpha) {
            Effect deck = EffectLoader.HackDeckPanel?.Value;
            if (deck != null) {
                deck.Parameters["uTime"]?.SetValue(timer);
                deck.Parameters["uAlpha"]?.SetValue(alpha * 0.92f);
                deck.Parameters["uResolution"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
                deck.Parameters["uTaperLeft"]?.SetValue(0f);
                deck.Parameters["uTaperRight"]?.SetValue(16f);
                deck.Parameters["uAccent"]?.SetValue(HackTheme.Accent.ToVector3());
                deck.Parameters["uHover"]?.SetValue(0f);
                deck.Parameters["uDisabled"]?.SetValue(0f);
                deck.Parameters["uProgress"]?.SetValue(0f);
                deck.Parameters["uGlitch"]?.SetValue(glitchIntensity * 0.6f);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, deck, Main.UIScaleMatrix);
                sb.Draw(px, panelRect, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
            }
            else {
                //CPU 回退暗底斜切 + CRT
                sb.Draw(px, panelRect, HackTheme.SrcPixel, HackTheme.BgPanel * (alpha * 0.9f));
                HackTheme.DrawCRTOverlay(sb, panelRect, alpha * 0.04f);
            }

            //开放框，右侧不封口
            float railBreathe = MathF.Sin(timer * 2.5f) * 0.1f + 0.9f;
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y + 4, 3, panelRect.Height - 8),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.55f * railBreathe));
            sb.Draw(px, new Rectangle(panelRect.X + 3, panelRect.Y + 4, 8, panelRect.Height - 8),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.05f));
            //顶线向右悬挑出面板
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width + 14, 1),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.40f));
            //底线只画左半
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width / 2, 1),
                HackTheme.SrcPixel, HackTheme.Border * (alpha * 0.6f));
            //左上角标
            HackTheme.DrawCornerBracket(sb, new Vector2(panelRect.X, panelRect.Y), 1, 1, 10, 1.4f,
                HackTheme.Accent * (alpha * 0.6f));
        }

        //DATA 高亮 / SCAN 暗置
        private void DrawTabs(SpriteBatch sb, float textX, float curY, float alpha, Rectangle panelRect) {
            Texture2D px = HackTheme.Pixel;
            string tab1 = $"//{HackTime.DataTab.Value}";
            Utils.DrawBorderString(sb, tab1, new Vector2((int)textX, (int)curY),
                Color.Lerp(HackTheme.Accent, Color.White, 0.15f) * alpha, 0.6f);
            float tab1W = FontAssets.MouseText.Value.MeasureString(tab1).X * 0.6f;
            //活动标签底线
            sb.Draw(px, new Rectangle((int)textX, (int)(curY + 17), (int)tab1W, 1),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.6f));

            string tab2 = HackTime.ScanTab.Value;
            HackTheme.DrawRawText(sb, tab2, new Vector2(textX + tab1W + 16, curY),
                HackTheme.TextNormal * (alpha * 0.6f), 0.6f);

            //右上微型 ID，无描边
            string idTag = $"ID:{(lastScanTarget?.GetHashCode() ?? 0) & 0xFFF:X3}";
            Vector2 idSize = FontAssets.MouseText.Value.MeasureString(idTag) * FontMicro;
            HackTheme.DrawRawText(sb, idTag, new Vector2(panelRect.Right - idSize.X - 12, curY + 2),
                HackTheme.TextNormal * (alpha * 0.55f), FontMicro);
        }

        #endregion

        #region 全息头像

        private void DrawPortrait(SpriteBatch sb, Texture2D px, Rectangle cell, float alpha) {
            //格子底与描边
            sb.Draw(px, cell, HackTheme.SrcPixel, HackTheme.BgDarkest * (alpha * 0.8f));
            Color cellEdge = HackTheme.Accent * (alpha * 0.35f);
            sb.Draw(px, new Rectangle(cell.X, cell.Y, cell.Width, 1), HackTheme.SrcPixel, cellEdge);
            sb.Draw(px, new Rectangle(cell.X, cell.Bottom - 1, cell.Width, 1), HackTheme.SrcPixel, cellEdge * 0.6f);
            sb.Draw(px, new Rectangle(cell.X, cell.Y, 1, cell.Height), HackTheme.SrcPixel, cellEdge * 0.8f);
            sb.Draw(px, new Rectangle(cell.Right - 1, cell.Y, 1, cell.Height), HackTheme.SrcPixel, cellEdge * 0.8f);

            //全息闪烁
            float flicker = 0.82f + 0.18f * MathF.Sin(timer * 27f + MathF.Sin(timer * 11f) * 2f);
            float holoAlpha = alpha * flicker;

            bool drewSprite = false;
            if (lastScanTarget is NpcScannable n && n.IsValid) {
                NPC npc = Main.npc[n.NpcIndex];
                Main.instance.LoadNPC(npc.type);
                Texture2D tex = TextureAssets.Npc[npc.type]?.Value;
                if (tex != null) {
                    Rectangle frame = npc.frame;
                    if (frame.Width <= 0 || frame.Height <= 0)
                        frame = new Rectangle(0, 0, tex.Width, tex.Height);
                    float fit = Math.Min((cell.Width - 14f) / frame.Width, (cell.Height - 14f) / frame.Height);
                    fit = Math.Min(fit, 2.4f);
                    Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
                    Vector2 pos = cell.Center.ToVector2();
                    SpriteEffects dir = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                    //色散残影
                    sb.Draw(tex, pos - new Vector2(1.6f, 0), frame, new Color(220, 40, 40) * (holoAlpha * 0.30f),
                        0f, origin, fit, dir, 0);
                    sb.Draw(tex, pos + new Vector2(1.6f, 0), frame, new Color(40, 120, 220) * (holoAlpha * 0.30f),
                        0f, origin, fit, dir, 0);
                    //主体单色全息
                    Color holoTint = Color.Lerp(HackTheme.Accent, Color.White, 0.35f);
                    sb.Draw(tex, pos, frame, holoTint * (holoAlpha * 0.85f), 0f, origin, fit, dir, 0);
                    drewSprite = true;
                }
            }

            if (!drewSprite) {
                //非 NPC 类别大字
                string glyph = KindGlyph(lastScanTarget?.TargetType?.Kind ?? HackTargetKind.None);
                Vector2 gs = FontAssets.MouseText.Value.MeasureString(glyph) * 1.4f;
                Utils.DrawBorderString(sb, glyph,
                    new Vector2(cell.Center.X - gs.X * 0.5f, cell.Center.Y - gs.Y * 0.5f),
                    HackTheme.Accent * (holoAlpha * 0.7f), 1.4f);
                HackTheme.DrawDiamondOutline(sb, cell.Center.ToVector2(), cell.Width * 0.34f, 1.2f,
                    HackTheme.Accent * (holoAlpha * 0.35f));
            }

            //格内扫描横纹与滚动亮线
            HackTheme.DrawCRTOverlay(sb, cell, alpha * 0.12f);
            float scanT = timer * 0.8f % 1f;
            int scanY = cell.Y + (int)(scanT * cell.Height);
            sb.Draw(px, new Rectangle(cell.X + 1, scanY, cell.Width - 2, 1),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.20f * (1f - Math.Abs(scanT - 0.5f) * 2f)));

            //已标记戳记（格底徽章）
            float stampPulse = MathF.Sin(timer * 4f) * 0.15f + 0.85f;
            HackTheme.DrawBadge(sb, new Vector2(cell.X + 2, cell.Bottom - 19),
                HackTime.TargetTagged.Value, HackTheme.Accent, alpha * stampPulse, 0.5f);
        }

        private static string KindGlyph(HackTargetKind kind) => kind switch {
            HackTargetKind.Tile => "▣",
            HackTargetKind.Turret => "◇",
            HackTargetKind.SignalTower => "◎",
            HackTargetKind.Projectile => "»",
            HackTargetKind.Water => "≈",
            HackTargetKind.Item => "●",
            _ => "◆",
        };

        //威胁菱形刻度（仅NPC，其余留空）
        private void DrawThreatPips(SpriteBatch sb, Vector2 pos, float alpha) {
            if (lastScanTarget is not NpcScannable n || !n.IsValid) return;
            int pips = NpcScannable.ComputeThreatPips(Main.npc[n.NpcIndex]);

            Utils.DrawBorderString(sb, HackTime.ThreatLabel.Value, new Vector2((int)pos.X, (int)pos.Y),
                HackTheme.TextNormal * (alpha * 0.85f), FontMicro);
            float labelW = FontAssets.MouseText.Value.MeasureString(HackTime.ThreatLabel.Value).X * FontMicro;

            Color pipOn = pips >= 4 ? HackTheme.Danger : pips >= 3 ? HackTheme.Uploading : HackTheme.Accent;
            for (int i = 0; i < 5; i++) {
                Vector2 c = new(pos.X + labelW + 14 + i * 14f, pos.Y + 8f);
                if (i < pips) {
                    float pulse = pips >= 4 ? MathF.Sin(timer * 5f + i) * 0.2f + 0.8f : 1f;
                    HackTheme.DrawDiamond(sb, c, 8f, pipOn * (alpha * 0.95f * pulse));
                }
                else {
                    HackTheme.DrawDiamondOutline(sb, c, 4f, 1f, HackTheme.BorderBright * (alpha * 0.7f));
                }
            }
        }

        #endregion

        #region 数据行与扫描阶段

        //扫描阶段 UI
        private void DrawScanPhase(SpriteBatch sb, Texture2D px, float baseX, float curY, float alpha) {
            float barX = baseX + 14;
            float barW = PanelWidth - 28;
            int barH = 6;

            //进度条背景
            sb.Draw(px, new Rectangle((int)barX, (int)curY, (int)barW, barH),
                HackTheme.SrcPixel, HackTheme.ProgressBg * alpha);

            //填充
            int fillW = (int)(barW * scanProgress);
            if (fillW > 0) {
                sb.Draw(px, new Rectangle((int)barX, (int)curY, fillW, barH),
                    HackTheme.SrcPixel, HackTheme.ProgressFill * (alpha * 0.85f));
                sb.Draw(px, new Rectangle((int)barX, (int)curY, fillW, 1),
                    HackTheme.SrcPixel, HackTheme.TextBright * (alpha * 0.2f));

                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Color tipGlow = HackTheme.ProgressGlow * (alpha * 0.35f);
                    tipGlow.A = 0;
                    sb.Draw(glow, new Vector2(barX + fillW, curY + barH * 0.5f), null,
                        tipGlow, 0, glow.Size() / 2, new Vector2(0.1f, 0.03f), SpriteEffects.None, 0);
                }
            }

            //扫描状态文字
            curY += 18f;
            string scanText = $"{HackTime.Scanning.Value} {(int)(scanProgress * 100)}%";
            float pulse = MathF.Sin(timer * 6f) * 0.2f + 0.8f;
            Utils.DrawBorderString(sb, scanText, new Vector2((int)(baseX + 14), (int)curY),
                Color.Lerp(HackTheme.Uploading, Color.White, 0.15f) * (alpha * pulse), 0.64f);

            //滚动噪声，无描边
            curY += 24f;
            string noise = $"0x{(int)(timer * 200) % 0xFFFFFF:X6}  "
                + $"BUF:{(int)(timer * 80) % 999:D3}  "
                + $"SIG:{(int)(timer * 150) % 0xFFF:X3}";
            HackTheme.DrawRawText(sb, noise, new Vector2(baseX + 14, curY),
                HackTheme.TextNormal * (alpha * 0.55f), 0.6f);
        }

        //单行数据渲染，maxWidth 限制数值起始偏移
        private void DrawDataRow(SpriteBatch sb, Texture2D px, float textX, float curY, int i, float alpha, float maxWidth) {
            bool isCurrent = i == revealedRows - 1;
            string label = rowLabels[i] ?? "";
            string value = rowValues[i] ?? "";
            Color valueColor = rowColors[i];

            //打字机截断
            string visibleValue;
            if (isCurrent && typewriterChar < value.Length)
                visibleValue = value[..(int)typewriterChar];
            else
                visibleValue = value;

            //揭示时的行内抖动
            float rowGlitch = 0f;
            if (isCurrent && typewriterChar < value.Length * 0.5f)
                rowGlitch = (1f - typewriterChar / Math.Max(value.Length, 1)) * 4f;
            float rowShake = rowGlitch * MathF.Sin(timer * 50f + i * 7f);

            //标签（微型，行首刻点）
            sb.Draw(px, new Rectangle((int)(textX + rowShake), (int)(curY + 7), 3, 3),
                HackTheme.SrcPixel, valueColor * (alpha * 0.6f));
            Utils.DrawBorderString(sb, label, new Vector2((int)(textX + 8 + rowShake), (int)(curY + 1)),
                HackTheme.TextNormal * (alpha * 0.9f), FontLabel);

            //数值（标签右侧固定列）
            float valueX = textX + Math.Min(maxWidth * 0.45f, 108f) + rowShake;

            //色散(揭示中)
            if (isCurrent && typewriterChar < value.Length) {
                float aberr = (1f - typewriterChar / Math.Max(value.Length, 1)) * 1.5f;
                Utils.DrawBorderString(sb, visibleValue, new Vector2(valueX - aberr, curY),
                    new Color(220, 40, 40) * (alpha * 0.15f), FontRow);
                Utils.DrawBorderString(sb, visibleValue, new Vector2(valueX + aberr, curY + 0.3f),
                    new Color(40, 80, 220) * (alpha * 0.15f), FontRow);
            }

            Utils.DrawBorderString(sb, visibleValue, new Vector2(valueX, curY),
                valueColor * alpha, FontRow);

            //闪烁光标
            if (isCurrent && typewriterChar < value.Length && (int)(timer * 10f) % 2 == 0) {
                float cursorX = valueX + FontAssets.MouseText.Value.MeasureString(visibleValue).X * FontRow + 1;
                Utils.DrawBorderString(sb, "▌", new Vector2(cursorX, curY),
                    HackTheme.Accent * (alpha * 0.5f), FontRow);
            }
        }

        #endregion

        #region 覆盖层

        //面板内竖向扫描线
        private void DrawScanLineOverlay(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            float scanT = timer * 1.2f % 1f;
            float scanY = rect.Y + scanT * rect.Height;
            float scanFade = 1f - Math.Abs(scanT - 0.5f) * 2f;
            sb.Draw(px, new Rectangle(rect.X + 2, (int)scanY, rect.Width - 4, 1),
                HackTheme.SrcPixel, HackTheme.Accent * (alpha * 0.08f * scanFade));
        }

        //面板外发光
        private static void DrawOuterGlow(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Color panelGlow = HackTheme.Accent * (alpha * 0.03f);
            panelGlow.A = 0;
            sb.Draw(glow, rect.Center.ToVector2(), null, panelGlow, 0,
                glow.Size() / 2, new Vector2(rect.Width / 25f, rect.Height / 25f),
                SpriteEffects.None, 0);
        }

        #endregion
    }
}
