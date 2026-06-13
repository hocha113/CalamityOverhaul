using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>目标扫描信息面板渲染器</summary>
    internal class ScanInfoRenderer
    {
        //上一帧扫描目标
        private IScannable lastScanTarget;
        //扫描目标数据行数
        private int currentDataRowCount;
        //扫描进度(0~1)
        private float scanProgress;
        //扫描完成后的计时器(秒)
        private float revealTimer;
        //已展开的数据行数
        private int revealedRows;
        //当前行打字机字符进度
        private float typewriterChar;
        //全局计时器
        private float timer;
        //飞入进度(0~1)
        private float flyInProgress;
        //故障抖动强度
        private float glitchIntensity;

        //布局参数
        public static float PanelWidth => 420f;
        public static float RowHeight => 22f;
        public static float HeaderHeight => 34f;
        public static float SepHeight => 10f;
        public static float StatusHeight => 28f;
        public static float GapToList => 18f;
        public static float TopPad => 10f;
        public static float BottomPad => 10f;
        //扫描时长(帧)
        public static float ScanDuration => 30f;
        //每行揭示间隔(秒)
        public static float RowRevealInterval => 0.13f;
        //打字机速度(字符/帧)
        public static float TypewriterSpeed => 2.5f;
        //数据行数组容量
        private const int MaxDataRowCount = 10;
        //字体大小
        public static float FontHeader => 0.90f;
        public static float FontRow => 0.80f;
        public static float FontStatus => 0.80f;
        public static float FontNoise => 0.70f;
        //面板纵向锚点偏移倍率
        public static float PanelVerticalOffsetRatio => 0.12f;

        //缓存的扫描数据
        private readonly string[] rowLabels = new string[MaxDataRowCount];
        private readonly string[] rowValues = new string[MaxDataRowCount];
        private readonly Color[] rowColors = new Color[MaxDataRowCount];
        private string statusText = "";
        private Color statusColor;

        public void Update() {
            timer += 0.016f;

            IScannable currentTarget = HackTime.CurrentScanTarget;

            //目标切换时重置扫描
            if (currentTarget != lastScanTarget) {
                lastScanTarget = currentTarget;
                if (currentTarget != null) {
                    currentDataRowCount = currentTarget.ScanRowCount;
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
                string val = rowValues[revealedRows - 1];
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

        public void Draw(SpriteBatch sb) {
            if (lastScanTarget == null && flyInProgress < 0.01f) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float alpha = HackTime.Intensity * flyInProgress;
            if (alpha < 0.01f) return;

            //面板位置
            int protocolCount = QuickHackDef.Count;
            float listTotalH = protocolCount * (78f + 5f) - 5f;
            float panelH = TopPad + HeaderHeight + SepHeight
                + currentDataRowCount * RowHeight + SepHeight + StatusHeight + BottomPad;
            float baseX = Main.screenWidth / 2 - PanelWidth / 2;
            float desiredTop = Main.screenHeight * 0.5f + listTotalH * PanelVerticalOffsetRatio;
            float panelTop = Math.Min(desiredTop, Main.screenHeight - panelH - 6f);

            //飞入偏移
            float flyOffset = (1f - EaseOutCubic(flyInProgress)) * 300f;
            baseX += flyOffset;

            //故障抖动
            float shakeX = glitchIntensity * MathF.Sin(timer * 45f) * 3f;
            float shakeY = glitchIntensity * MathF.Cos(timer * 38f) * 1.5f;
            baseX += shakeX;
            panelTop += shakeY;

            Rectangle panelRect = new((int)baseX, (int)panelTop, (int)PanelWidth, (int)panelH);

            //面板背景
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), HackTheme.BgPanel * (alpha * 0.88f));

            //底部渐亮
            int gradH = panelRect.Height / 3;
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - gradH, panelRect.Width, gradH),
                new Rectangle(0, 0, 1, 1), HackTheme.BgSlotHover * (alpha * 0.15f));

            //CRT暗纹
            DrawCRTOverlay(sb, px, panelRect, alpha * 0.04f);

            //边框
            Color borderCol = Color.Lerp(HackTheme.Border, HackTheme.Accent, 0.2f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.4f));
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.35f));
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height),
                new Rectangle(0, 0, 1, 1), borderCol * (alpha * 0.35f));

            //角标括号
            DrawCornerBrackets(sb, px, panelRect, alpha, HackTheme.Accent);

            //左侧强调竖条
            float barBreathe = MathF.Sin(timer * 2.5f) * 0.1f + 0.9f;
            sb.Draw(px, new Rectangle(panelRect.X + 3, panelRect.Y + 5, 3, panelRect.Height - 10),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * (alpha * 0.3f * barBreathe));

            //顶部高光线
            sb.Draw(px, new Rectangle(panelRect.X + 4, panelRect.Y + 1, panelRect.Width - 8, 1),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * (alpha * 0.12f));

            float curY = panelTop + TopPad;
            float textX = baseX + 14f;

            //标题
            string header = "// SCAN ANALYSIS";
            int headerChars = scanProgress < 1f
                ? (int)(header.Length * Math.Min(scanProgress * 2.5f, 1f))
                : header.Length;
            headerChars = Math.Clamp(headerChars, 0, header.Length);
            string visibleHeader = header[..headerChars];
            Utils.DrawBorderString(sb, visibleHeader, new Vector2(textX, curY),
                HackTheme.Accent * (alpha * 0.75f), FontHeader);

            //闪烁光标
            if (headerChars < header.Length || scanProgress < 1f && (int)(timer * 8f) % 2 == 0) {
                float cursorX = textX + FontAssets.MouseText.Value.MeasureString(visibleHeader).X * FontHeader + 2;
                Utils.DrawBorderString(sb, "█", new Vector2(cursorX, curY),
                    HackTheme.Accent * (alpha * 0.55f), FontHeader);
            }

            curY += HeaderHeight;

            //分隔线
            DrawDashedLine(sb, px, baseX + 10, curY, PanelWidth - 20, alpha);
            curY += SepHeight;

            //扫描阶段
            if (scanProgress < 1f) {
                DrawScanPhase(sb, px, baseX, curY, alpha);
                DrawScanLineOverlay(sb, px, panelRect, alpha);
                DrawOuterGlow(sb, panelRect, alpha);
                return;
            }

            //数据行
            for (int i = 0; i < currentDataRowCount; i++) {
                if (i >= revealedRows) break;
                DrawDataRow(sb, px, textX, curY, i, alpha);
                curY += RowHeight;
            }

            //底部分隔与状态
            if (revealedRows >= currentDataRowCount) {
                DrawDashedLine(sb, px, baseX + 10, curY, PanelWidth - 20, alpha);
                curY += SepHeight;

                float statusPulse = MathF.Sin(timer * 3f) * 0.15f + 0.85f;
                Utils.DrawBorderString(sb, statusText, new Vector2(textX, curY),
                    statusColor * (alpha * statusPulse), FontStatus);

                string hexTag = $"0x{(int)(timer * 50) % 0xFFFF:X4}";
                Utils.DrawBorderString(sb, hexTag, new Vector2(baseX + PanelWidth - 96, curY),
                    HackTheme.Accent * (alpha * 0.22f), 0.44f);
            }

            DrawScanLineOverlay(sb, px, panelRect, alpha);
            DrawOuterGlow(sb, panelRect, alpha);
        }

        #region 内部绘制

        //扫描阶段 UI
        private void DrawScanPhase(SpriteBatch sb, Texture2D px, float baseX, float curY, float alpha) {
            float barX = baseX + 14;
            float barW = PanelWidth - 28;
            int barH = 6;

            //进度条背景
            sb.Draw(px, new Rectangle((int)barX, (int)curY, (int)barW, barH),
                new Rectangle(0, 0, 1, 1), HackTheme.ProgressBg * alpha);

            //填充
            int fillW = (int)(barW * scanProgress);
            if (fillW > 0) {
                sb.Draw(px, new Rectangle((int)barX, (int)curY, fillW, barH),
                    new Rectangle(0, 0, 1, 1), HackTheme.ProgressFill * (alpha * 0.85f));
                sb.Draw(px, new Rectangle((int)barX, (int)curY, fillW, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.TextBright * (alpha * 0.2f));

                //前端辉光
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
            string scanText = $"SCANNING... {(int)(scanProgress * 100)}%";
            float pulse = MathF.Sin(timer * 6f) * 0.2f + 0.8f;
            Utils.DrawBorderString(sb, scanText, new Vector2(baseX + 14, curY),
                HackTheme.Uploading * (alpha * pulse), 0.60f);

            //滚动数据噪声
            curY += 22f;
            string noise = $"0x{(int)(timer * 200) % 0xFFFFFF:X6}  "
                + $"BUF:{(int)(timer * 80) % 999:D3}  "
                + $"SIG:{(int)(timer * 150) % 0xFFF:X3}";
            Utils.DrawBorderString(sb, noise, new Vector2(baseX + 14, curY),
                HackTheme.TextDim * (alpha * 0.3f), FontNoise);
        }

        //单行数据渲染
        private void DrawDataRow(SpriteBatch sb, Texture2D px, float textX, float curY, int i, float alpha) {
            bool isCurrent = i == revealedRows - 1;
            string label = rowLabels[i];
            string value = rowValues[i];
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
                rowGlitch = (1f - typewriterChar / value.Length) * 4f;
            float rowShake = rowGlitch * MathF.Sin(timer * 50f + i * 7f);

            //标签
            string labelText = $"◆ {label}";
            Utils.DrawBorderString(sb, labelText, new Vector2(textX + rowShake, curY),
                HackTheme.TextDim * (alpha * 0.7f), FontRow);

            //数值
            float valueX = textX + 110f + rowShake;

            //色散(揭示中)
            if (isCurrent && typewriterChar < value.Length) {
                float aberr = (1f - typewriterChar / value.Length) * 1.5f;
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

            //已完成行的呼吸指示点
            if (!isCurrent || typewriterChar >= value.Length) {
                float dotPulse = MathF.Sin(timer * 2f + i * 1.2f) * 0.2f + 0.8f;
                sb.Draw(px, new Vector2(textX - 8, curY + 5),
                    new Rectangle(0, 0, 1, 1), valueColor * (alpha * 0.25f * dotPulse),
                    0, Vector2.Zero, 3f, SpriteEffects.None, 0);
            }
        }

        //面板内竖向扫描线
        private void DrawScanLineOverlay(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            float scanT = timer * 1.2f % 1f;
            float scanY = rect.Y + scanT * rect.Height;
            float scanFade = 1f - Math.Abs(scanT - 0.5f) * 2f;
            sb.Draw(px, new Rectangle(rect.X + 2, (int)scanY, rect.Width - 4, 1),
                new Rectangle(0, 0, 1, 1), HackTheme.Accent * (alpha * 0.08f * scanFade));
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

        #region 工具方法

        private static void DrawCRTOverlay(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha) {
            Color line = HackTheme.BgDarkest * alpha;
            for (int dy = 0; dy < rect.Height; dy += 3)
                sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, rect.Width, 1),
                    new Rectangle(0, 0, 1, 1), line);
        }

        private static void DrawDashedLine(SpriteBatch sb, Texture2D px, float x, float y, float width, float alpha) {
            for (float dx = 0; dx < width; dx += 6)
                sb.Draw(px, new Rectangle((int)(x + dx), (int)y, 3, 1),
                    new Rectangle(0, 0, 1, 1), HackTheme.Border * (alpha * 0.35f));
        }

        private static void DrawCornerBrackets(SpriteBatch sb, Texture2D px, Rectangle rect, float alpha, Color color) {
            int arm = 10;
            Color c = color * (alpha * 0.4f);
            //左上
            sb.Draw(px, new Rectangle(rect.X, rect.Y, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //右上
            sb.Draw(px, new Rectangle(rect.Right - arm, rect.Y, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //左下
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - arm, 1, arm), new Rectangle(0, 0, 1, 1), c);
            //右下
            sb.Draw(px, new Rectangle(rect.Right - arm, rect.Bottom - 1, arm, 1), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Bottom - arm, 1, arm), new Rectangle(0, 0, 1, 1), c);
        }

        private static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        #endregion
    }
}
