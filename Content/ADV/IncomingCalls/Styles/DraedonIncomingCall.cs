using CalamityOverhaul.Content.ADV.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.IncomingCalls.Styles
{
    /// <summary>
    /// 嘉登科技来电 UI，L 角框、均衡器、扰码打字与通话时长
    /// </summary>
    internal class DraedonIncomingCall : IncomingCallBase, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static DraedonIncomingCall Instance => UIHandleLoader.GetUIHandleOfType<DraedonIncomingCall>();

        #region 本地化文本

        public static LocalizedText IncomingLabel { get; private set; }
        public static LocalizedText AnswerHint { get; private set; }
        public static LocalizedText ConnectingLabel { get; private set; }
        public static LocalizedText EndLabel { get; private set; }
        public static LocalizedText NextLabel { get; private set; }
        public static LocalizedText TimerLabel { get; private set; }
        public static LocalizedText UnknownCaller { get; private set; }
        public static LocalizedText ChannelPrefix { get; private set; }

        public override void SetStaticDefaults() {
            IncomingLabel = this.GetLocalization(nameof(IncomingLabel), () => "来电");
            AnswerHint = this.GetLocalization(nameof(AnswerHint), () => "[Z] 接听");
            ConnectingLabel = this.GetLocalization(nameof(ConnectingLabel), () => "连接中...");
            EndLabel = this.GetLocalization(nameof(EndLabel), () => "通话结束");
            NextLabel = this.GetLocalization(nameof(NextLabel), () => "[Z] 继续");
            TimerLabel = this.GetLocalization(nameof(TimerLabel), () => "通话时长");
            UnknownCaller = this.GetLocalization(nameof(UnknownCaller), () => "未知来电");
            ChannelPrefix = this.GetLocalization(nameof(ChannelPrefix), () => "CH.");
        }

        #endregion

        #region 动画参数

        private float scanLinePos;
        private float scanLineSpeed;
        private float circuitPulseTimer;
        private float hologramFlicker;
        private float signalArcTimer;
        private float answerBtnPulse;
        private float callIndicatorBlink;
        private float glitchTimer;

        private readonly float[] eqBars = new float[10];
        private readonly float[] eqTargets = new float[10];
        private int eqUpdateTimer;
        private float connectingTimer;

        private readonly List<DraedonDataPRT> dataParticles = [];
        private int dataParticleSpawnTimer;
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int circuitNodeSpawnTimer;

        private const int ScrambleDepth = 3;
        private static readonly char[] ScramblePool
            = "!?><[]{}#@$%^01┤├╫▓░".ToCharArray();

        #endregion

        #region 样式参数

        protected override float RingingPanelWidth => 280f;
        protected override float RingingPanelHeight => 118f;
        protected override float SpeakingPanelWidth => 410f;
        protected override float SpeakingPanelHeight => 230f;
        protected override float LeftMargin => 24f;
        protected override float TopRatio => 0.22f;
        protected override float PortraitSize => 56f;
        protected override float TextScale => 0.82f;
        protected override float NameScale => 0.9f;
        protected override int TypeInterval => 2;
        protected override int AutoHangUpDelay => 120;

        #endregion

        #region 生命周期

        protected override void OnCallStarted() {
            scanLinePos = 0f;
            scanLineSpeed = 0f;
            circuitPulseTimer = 0f;
            hologramFlicker = 0f;
            signalArcTimer = 0f;
            answerBtnPulse = 0f;
            callIndicatorBlink = 0f;
            glitchTimer = 0f;
            connectingTimer = 0f;
            eqUpdateTimer = 0;
            for (int i = 0; i < eqBars.Length; i++) {
                eqBars[i] = 0f;
                eqTargets[i] = 0f;
            }
            dataParticles.Clear();
            circuitNodes.Clear();
            dataParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
        }

        #endregion

        #region 字符覆写（扰码效果）

        protected override char? GetCharOverride(int globalCharIndex) {
            if (!finishedCurrent
                && globalCharIndex >= visibleCharCount - ScrambleDepth
                && globalCharIndex < visibleCharCount) {
                int seed = (int)(circuitPulseTimer * 137f) + globalCharIndex * 31;
                return ScramblePool[Math.Abs(seed) % ScramblePool.Length];
            }
            return null;
        }

        #endregion

        #region 更新

        protected override void StyleUpdate() {
            WrapTimer(ref circuitPulseTimer, 0.025f);
            WrapTimer(ref hologramFlicker, 0.13f);
            WrapTimer(ref signalArcTimer, 0.06f);
            WrapTimer(ref answerBtnPulse, 0.08f);
            WrapTimer(ref callIndicatorBlink, 0.04f);
            glitchTimer += 0.17f;
            if (glitchTimer > MathHelper.TwoPi) glitchTimer -= MathHelper.TwoPi;

            // 扫描线滚动
            scanLineSpeed = MathHelper.Lerp(scanLineSpeed, 0.006f, 0.015f);
            scanLinePos += scanLineSpeed;
            if (scanLinePos > 1f) scanLinePos -= 1f;

            connectingTimer += 0.04f;
            if (connectingTimer > MathHelper.TwoPi) connectingTimer -= MathHelper.TwoPi;

            // 均衡器（通话中）
            if (State == IncomingCallState.Speaking) {
                eqUpdateTimer++;
                if (eqUpdateTimer >= 7) {
                    eqUpdateTimer = 0;
                    for (int i = 0; i < eqTargets.Length; i++)
                        eqTargets[i] = Main.rand.NextFloat(0.15f, 1f);
                }
                for (int i = 0; i < eqBars.Length; i++)
                    eqBars[i] = MathHelper.Lerp(eqBars[i], eqTargets[i], 0.22f);
            }

            Rectangle panelRect = GetCurrentPanelRect();
            Vector2 panelPos = new(panelRect.X, panelRect.Y);
            Vector2 panelSize = new(panelRect.Width, panelRect.Height);

            // 数据粒子
            dataParticleSpawnTimer++;
            if (State != IncomingCallState.Idle && State != IncomingCallState.Ending
                && dataParticleSpawnTimer >= 22 && dataParticles.Count < 8) {
                dataParticleSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(
                    Main.rand.NextFloat(12f, panelSize.X - 12f),
                    Main.rand.NextFloat(12f, panelSize.Y - 12f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(panelPos, panelSize))
                    dataParticles.RemoveAt(i);
            }

            // 电路节点
            circuitNodeSpawnTimer++;
            if (State != IncomingCallState.Idle && State != IncomingCallState.Ending
                && circuitNodeSpawnTimer >= 32 && circuitNodes.Count < 4) {
                circuitNodeSpawnTimer = 0;
                Vector2 start = panelPos + new Vector2(
                    Main.rand.NextFloat(12f, panelSize.X - 12f),
                    Main.rand.NextFloat(12f, panelSize.Y - 12f));
                circuitNodes.Add(new CircuitNodePRT(start));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update())
                    circuitNodes.RemoveAt(i);
            }
        }

        private static void WrapTimer(ref float timer, float speed) {
            timer += speed;
            if (timer > MathHelper.TwoPi) timer -= MathHelper.TwoPi;
        }

        #endregion

        #region 振铃绘制

        protected override void DrawRingingPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            float flicker = 0.92f + MathF.Sin(hologramFlicker * 2f) * 0.08f;
            float fa = alpha * flicker;

            DrawShadow(sb, rect, alpha);
            DrawTechBackground(sb, rect, fa);
            DrawHexGrid(sb, rect, fa * 0.35f);
            DrawBracketFrame(sb, rect, fa);
            DrawScanLine(sb, rect, fa);
            DrawParticles(sb, fa);

            // 信号格
            DrawSignalBars(sb, new Vector2(rect.Right - 44f, rect.Y + 8f), fa);

            // 来电标题栏
            float titleBlink = MathF.Sin(answerBtnPulse * 2.4f) * 0.4f + 0.6f;
            Utils.DrawBorderString(sb, IncomingLabel.Value,
                new Vector2(rect.X + 12f, rect.Y + 8f),
                new Color(0, 220, 255) * (fa * titleBlink), 0.78f);

            // 标题下分割线
            DrawHLine(sb, rect.X + 10, rect.Y + 26, rect.Width - 20, new Color(0, 180, 255) * (fa * 0.55f));

            // 头像区
            float portraitDrawSize = PortraitSize;
            Vector2 portraitCenter = new(rect.X + 14f + portraitDrawSize / 2f,
                                         rect.Y + 37f + portraitDrawSize / 2f);
            DrawPortraitFrame(sb, portraitCenter, portraitDrawSize, fa);
            DrawPortrait(sb, portraitCenter, portraitDrawSize * 0.84f, fa);
            DrawSignalArcs(sb, portraitCenter, portraitDrawSize, fa);

            // 来电者名称与接听提示
            float textX = rect.X + 14f + portraitDrawSize + 16f;
            float nameY = rect.Y + 34f;
            Utils.DrawBorderString(sb, callerName ?? UnknownCaller.Value,
                new Vector2(textX, nameY), new Color(80, 220, 255) * fa, NameScale);

            float hintBlink = MathF.Sin(answerBtnPulse * 1.8f) * 0.45f + 0.55f;
            Utils.DrawBorderString(sb, AnswerHint.Value,
                new Vector2(textX, nameY + 28f),
                new Color(100, 200, 255) * (fa * 0.72f * hintBlink), 0.68f);

            // 底部接听条
            DrawAnswerBar(sb, rect, fa);
        }

        private void DrawAnswerBar(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float pulse = MathF.Sin(answerBtnPulse * 2f) * 0.5f + 0.5f;
            int barH = 20;
            Rectangle barRect = new(rect.X + 8, rect.Bottom - barH - 6, rect.Width - 16, barH);
            sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), new Color(0, 60, 100) * (alpha * 0.6f * pulse));
            DrawRectBorder(sb, barRect, 1, new Color(0, 200, 255) * (alpha * 0.65f * pulse));
            string answerText = $"[ {AnswerHint.Value} ]";
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(answerText) * 0.65f;
            Utils.DrawBorderString(sb, answerText,
                new Vector2(barRect.X + (barRect.Width - textSize.X) / 2f,
                             barRect.Y + (barRect.Height - textSize.Y) / 2f - 1f),
                new Color(160, 240, 255) * (alpha * pulse), 0.65f);
        }

        #endregion

        #region 通话绘制

        protected override void DrawSpeakingPanel(SpriteBatch sb, Rectangle rect, float alpha, float contentAlpha) {
            float flicker = 0.92f + MathF.Sin(hologramFlicker * 2f) * 0.08f;
            float fa = alpha * flicker;

            DrawShadow(sb, rect, alpha);
            DrawTechBackground(sb, rect, fa);
            DrawHexGrid(sb, rect, fa * 0.28f);
            DrawBracketFrame(sb, rect, fa);
            DrawScanLine(sb, rect, fa);
            DrawParticles(sb, fa);

            // 连接过渡遮罩
            if (expandProgress < 0.98f) {
                DrawConnectingOverlay(sb, rect, fa, expandProgress);
                return;
            }

            // 通话状态圆点
            float indicBlink = MathF.Sin(callIndicatorBlink * 3f) * 0.5f + 0.5f;
            DrawFilledDot(sb, new Vector2(rect.Right - 14f, rect.Y + 13f), 4f,
                new Color(40, 255, 130) * (fa * indicBlink));

            // 信号格与通话时长
            DrawSignalBars(sb, new Vector2(rect.Right - 44f, rect.Y + 8f), fa);
            DrawCallTimer(sb, rect, fa * contentAlpha);

            // 头像区
            float portraitDrawSize = PortraitSize;
            Vector2 portraitCenter = new(rect.X + 14f + portraitDrawSize / 2f,
                                         rect.Y + 14f + portraitDrawSize / 2f);
            DrawPortraitFrame(sb, portraitCenter, portraitDrawSize, fa);
            DrawPortrait(sb, portraitCenter, portraitDrawSize * 0.84f, fa);

            // 名称与频道
            float nameX = rect.X + 14f + portraitDrawSize + 14f;
            float nameY = rect.Y + 14f;
            string speakerName = current?.Speaker ?? callerName ?? UnknownCaller.Value;
            string channelSuffix = $" #{ChannelPrefix.Value}-01";
            Color nameColor = new Color(80, 220, 255) * (fa * contentAlpha);
            Utils.DrawBorderString(sb, speakerName, new Vector2(nameX, nameY), nameColor, NameScale);
            Utils.DrawBorderString(sb, channelSuffix,
                new Vector2(nameX + FontAssets.MouseText.Value.MeasureString(speakerName).X * NameScale, nameY + 3f),
                new Color(40, 160, 200) * (fa * contentAlpha * 0.65f), 0.7f);

            // 渐变分割线
            DrawAnimatedDivider(sb, nameX, nameY + 22f,
                rect.Width - (int)(nameX - rect.X) - 14, fa * contentAlpha);

            // 台词文本
            if (wrappedLines != null && wrappedLines.Length > 0) {
                Vector2 textStart = new(nameX, nameY + 34f);
                Color textColor = Color.Lerp(new Color(200, 240, 255), Color.White, 0.25f);
                DrawTypedText(sb, textStart, contentAlpha * fa, textColor);
            }

            // 底部 Footer
            DrawSpeakingFooter(sb, rect, fa, contentAlpha);
        }

        private void DrawConnectingOverlay(SpriteBatch sb, Rectangle rect, float alpha, float progress) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 半透明遮罩
            Color overlayC = new Color(4, 12, 24) * (alpha * (1f - progress) * 0.75f);
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), overlayC);

            // 连接中文字动画
            int dots = (int)(connectingTimer / (MathHelper.TwoPi / 4f)) % 4;
            string connectText = ConnectingLabel.Value + new string('.', dots);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(connectText) * 0.88f;
            Vector2 textPos = new(rect.X + (rect.Width - textSize.X) / 2f,
                                   rect.Y + (rect.Height - textSize.Y) / 2f - 12f);
            float connectPulse = MathF.Sin(connectingTimer * 1.4f) * 0.3f + 0.7f;
            Utils.DrawBorderString(sb, connectText, textPos,
                new Color(0, 200, 255) * (alpha * connectPulse), 0.88f);

            // 连接进度条
            int barW = rect.Width - 40;
            int barH = 3;
            Rectangle barBg = new(rect.X + 20, (int)textPos.Y + 28, barW, barH);
            sb.Draw(px, barBg, new Rectangle(0, 0, 1, 1), new Color(0, 40, 80) * alpha);
            int fillW = (int)(barW * progress);
            if (fillW > 0) {
                sb.Draw(px, new Rectangle(rect.X + 20, (int)textPos.Y + 28, fillW, barH),
                    new Rectangle(0, 0, 1, 1), new Color(0, 220, 255) * alpha);
            }
        }

        private void DrawSpeakingFooter(SpriteBatch sb, Rectangle rect, float fa, float contentAlpha) {
            int footerY = rect.Bottom - 26;

            // 底部分割线
            DrawHLine(sb, rect.X + 8, footerY, rect.Width - 16,
                new Color(0, 120, 200) * (fa * 0.4f));

            // 均衡器
            DrawEqualizer(sb, new Vector2(rect.X + 10f, footerY + 20f), 18f, 80f, fa * contentAlpha);

            // 导航提示
            if (finishedCurrent && current != null) {
                float hintBlink = MathF.Sin(answerBtnPulse * 2.5f) * 0.45f + 0.55f;
                bool isLast = queue.Count == 0;
                string hint = isLast ? $"■ {EndLabel.Value}" : $"▶ {NextLabel.Value}";
                Color hintColor = isLast
                    ? new Color(255, 80, 80) * (fa * contentAlpha * hintBlink)
                    : new Color(80, 200, 255) * (fa * contentAlpha * hintBlink);
                Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.73f;
                Utils.DrawBorderString(sb, hint,
                    new Vector2(rect.Right - hintSize.X - 10f, footerY + 4f),
                    hintColor, 0.73f);
            }
        }

        #endregion

        #region 样式绘制工具

        private static void DrawShadow(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int d = 6; d >= 1; d--) {
                Rectangle s = rect;
                s.Inflate(d, d);
                s.Offset(3, 4);
                float a = alpha * 0.08f * (6f - d) / 6f;
                sb.Draw(px, s, new Rectangle(0, 0, 1, 1), Color.Black * a);
            }
        }

        private void DrawTechBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int segs = 24;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));
                float pulse = MathF.Sin(circuitPulseTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;
                Color dark = new Color(6, 10, 20);
                Color mid = new Color(16, 26, 42);
                Color c = Color.Lerp(dark, mid, pulse) * (alpha * 0.94f);
                sb.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            // 故障水平闪光
            float gFlash = MathF.Sin(glitchTimer * 3.7f);
            if (gFlash > 0.92f) {
                float glitchY = rect.Y + (glitchTimer * 107f % rect.Height);
                Rectangle gr = new(rect.X, (int)glitchY, rect.Width, 2);
                sb.Draw(px, gr, new Rectangle(0, 0, 1, 1),
                    new Color(40, 180, 255) * (alpha * (gFlash - 0.92f) * 2f));
            }
        }

        /// <summary>
        /// 六边形点阵背景纹理
        /// </summary>
        private static void DrawHexGrid(SpriteBatch sb, Rectangle rect, float alpha) {
            if (alpha < 0.01f) return;
            Texture2D px = VaultAsset.placeholder2.Value;
            Color c = new Color(40, 140, 220) * alpha;
            float cellW = 18f, cellH = 16f;
            int cols = (int)(rect.Width / cellW) + 2;
            int rows = (int)(rect.Height / cellH) + 2;
            for (int row = 0; row < rows; row++) {
                for (int col = 0; col < cols; col++) {
                    float ox = (row % 2 == 0) ? 0f : cellW * 0.5f;
                    float px2 = rect.X + col * cellW + ox;
                    float py = rect.Y + row * cellH;
                    if (px2 < rect.X - 2 || px2 > rect.Right + 2) continue;
                    if (py < rect.Y - 2 || py > rect.Bottom + 2) continue;
                    sb.Draw(px, new Rectangle((int)px2, (int)py, 1, 1),
                        new Rectangle(0, 0, 1, 1), c);
                }
            }
        }

        /// <summary>
        /// L 角框，四角独立线段
        /// </summary>
        private static void DrawBracketFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Color edge = new Color(40, 190, 255) * (alpha * 0.9f);
            Color edgeDim = edge * 0.6f;
            int bl = 22;
            int bw = 2;

            // 顶部高光线
            DrawHorLine(sb, rect.X, rect.Y, rect.Width, 1, edge * 0.45f);

            // 四角 L 括号
            DrawHorLine(sb, rect.X, rect.Y, bl, bw, edge);
            DrawVerLine(sb, rect.X, rect.Y, bl, bw, edge);
            DrawHorLine(sb, rect.Right - bl, rect.Y, bl, bw, edge);
            DrawVerLine(sb, rect.Right - bw, rect.Y, bl, bw, edge);
            DrawHorLine(sb, rect.X, rect.Bottom - bw, bl, bw, edgeDim);
            DrawVerLine(sb, rect.X, rect.Bottom - bl, bl, bw, edgeDim);
            DrawHorLine(sb, rect.Right - bl, rect.Bottom - bw, bl, bw, edgeDim);
            DrawVerLine(sb, rect.Right - bw, rect.Bottom - bl, bl, bw, edgeDim);

            // 角落刻痕
            DrawHorLine(sb, rect.X + bl + 2, rect.Y, 4, 1, edge * 0.35f);
            DrawHorLine(sb, rect.Right - bl - 6, rect.Y, 4, 1, edge * 0.35f);
        }

        private static void DrawHorLine(SpriteBatch sb, int x, int y, int w, int h, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        private static void DrawVerLine(SpriteBatch sb, int x, int y, int h, int w, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(x, y, w, h), new Rectangle(0, 0, 1, 1), c);
        }

        private static void DrawHLine(SpriteBatch sb, int x, int y, int w, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(x, y, w, 1), new Rectangle(0, 0, 1, 1), c);
        }

        private static void DrawRectBorder(SpriteBatch sb, Rectangle rect, int bw, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - bw, rect.Width, bw), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
            sb.Draw(px, new Rectangle(rect.Right - bw, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), c);
        }

        private static void DrawFilledDot(SpriteBatch sb, Vector2 center, float radius, Color c) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int r = (int)radius;
            sb.Draw(px, new Rectangle((int)(center.X - r), (int)(center.Y - r), r * 2, r * 2),
                new Rectangle(0, 0, 1, 1), c);
        }

        /// <summary>
        /// 带渐隐拖影的向上扫描线
        /// </summary>
        private void DrawScanLine(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + scanLinePos * rect.Height;

            // 扫描线拖影
            for (int i = 1; i <= 16; i++) {
                float trailY = scanY - i * 2f;
                if (trailY < rect.Y) continue;
                float trailA = 1f - i / 16f;
                Color trailCol = new Color(30, 140, 220) * (alpha * 0.055f * trailA);
                sb.Draw(px, new Rectangle(rect.X + 4, (int)trailY, rect.Width - 8, 1),
                    new Rectangle(0, 0, 1, 1), trailCol);
            }

            // 主扫描线
            Color scanColor = new Color(80, 210, 255) * (alpha * 0.3f);
            sb.Draw(px, new Rectangle(rect.X + 4, (int)scanY, rect.Width - 8, 2),
                new Rectangle(0, 0, 1, 1), scanColor);
        }

        /// <summary>
        /// 流动渐变分割线
        /// </summary>
        private void DrawAnimatedDivider(SpriteBatch sb, float x, float y, int w, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            int segs = Math.Max(1, w / 4);
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float flow = (t + circuitPulseTimer * 0.3f) % 1f;
                float bright = MathF.Sin(flow * MathHelper.Pi) * 0.6f + 0.4f;
                Color c = new Color(40, 160, 255) * (alpha * bright * 0.8f);
                sb.Draw(px, new Rectangle((int)(x + i * 4), (int)y, 3, 1),
                    new Rectangle(0, 0, 1, 1), c);
            }
        }

        /// <summary>
        /// 头像 L形角括号边框
        /// </summary>
        private static void DrawPortraitFrame(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float half = size / 2f + 4f;
            Rectangle frame = new((int)(center.X - half), (int)(center.Y - half),
                (int)(half * 2), (int)(half * 2));

            // 头像底
            Color bg = new Color(6, 14, 28) * (alpha * 0.88f);
            sb.Draw(px, frame, new Rectangle(0, 0, 1, 1), bg);

            // L 角括号
            Color edge = new Color(40, 170, 245) * (alpha * 0.75f);
            int bl = (int)(size * 0.3f);
            int bw = 2;
            DrawHorLine(sb, frame.X, frame.Y, bl, bw, edge);
            DrawVerLine(sb, frame.X, frame.Y, bl, bw, edge);
            DrawHorLine(sb, frame.Right - bl, frame.Y, bl, bw, edge);
            DrawVerLine(sb, frame.Right - bw, frame.Y, bl, bw, edge);
            DrawHorLine(sb, frame.X, frame.Bottom - bw, bl, bw, edge * 0.7f);
            DrawVerLine(sb, frame.X, frame.Bottom - bl, bl, bw, edge * 0.7f);
            DrawHorLine(sb, frame.Right - bl, frame.Bottom - bw, bl, bw, edge * 0.7f);
            DrawVerLine(sb, frame.Right - bw, frame.Bottom - bl, bl, bw, edge * 0.7f);
        }

        private void DrawSignalArcs(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            // 振铃脉冲环（3 段弧）
            foreach (float progress in ringPulses) {
                float radius = size * 0.5f + progress * size * 0.75f;
                float ringAlpha = (1f - progress) * alpha * 0.65f;
                Color ringColor = new Color(0, 200, 255) * ringAlpha;
                for (int arc = 0; arc < 3; arc++) {
                    float baseAngle = MathHelper.TwoPi / 3f * arc + signalArcTimer;
                    float arcLen = MathHelper.Pi * 0.38f;
                    int segments = 10;
                    for (int s = 0; s < segments; s++) {
                        float t = s / (float)segments;
                        float angle = baseAngle - arcLen / 2f + arcLen * t;
                        Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                        sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), ringColor * (1f - t * 0.4f),
                            0f, new Vector2(0.5f), new Vector2(2.8f), SpriteEffects.None, 0f);
                    }
                }
            }

            // 常驻信号弧（4 等分）
            float staticAlpha = MathF.Sin(signalArcTimer * 1.2f) * 0.25f + 0.35f;
            for (int arc = 0; arc < 4; arc++) {
                float baseAngle = MathHelper.TwoPi / 4f * arc + signalArcTimer * 0.45f;
                float arcLen = MathHelper.Pi * 0.2f;
                float radius = size * 0.54f;
                int segments = 5;
                for (int s = 0; s < segments; s++) {
                    float t = s / (float)segments;
                    float angle = baseAngle - arcLen / 2f + arcLen * t;
                    Vector2 pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    Color c = new Color(20, 140, 240) * (alpha * staticAlpha * (1f - t * 0.5f));
                    sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c,
                        0f, new Vector2(0.5f), new Vector2(2f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// 信号格，4 条递增高竖条
        /// </summary>
        private static void DrawSignalBars(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            for (int i = 0; i < 4; i++) {
                int barH = (i + 1) * 4;
                int barW = 4;
                Color c = new Color(0, 210, 160) * (alpha * 0.85f);
                sb.Draw(px, new Rectangle((int)pos.X + i * 6, (int)(pos.Y + 16 - barH), barW, barH),
                    new Rectangle(0, 0, 1, 1), c);
            }
        }

        /// <summary>
        /// 通话时长 MM:SS
        /// </summary>
        private void DrawCallTimer(SpriteBatch sb, Rectangle rect, float alpha) {
            int totalSec = callDurationFrames / 60;
            string timerText = $"{TimerLabel.Value} {totalSec / 60:00}:{totalSec % 60:00}";
            Color c = new Color(60, 180, 220) * (alpha * 0.6f);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(timerText) * 0.62f;
            Utils.DrawBorderString(sb, timerText,
                new Vector2(rect.Right - textSize.X - 10f, rect.Y + 8f), c, 0.62f);
        }

        /// <summary>
        /// 均衡器条形图
        /// </summary>
        private void DrawEqualizer(SpriteBatch sb, Vector2 origin, float maxH, float totalW, float alpha) {
            if (alpha < 0.01f) return;
            Texture2D px = VaultAsset.placeholder2.Value;
            int n = eqBars.Length;
            float barW = totalW / n - 1.5f;
            for (int i = 0; i < n; i++) {
                float h = eqBars[i] * maxH;
                float x = origin.X + i * (barW + 1.5f);
                float y = origin.Y - h;
                Color c = Color.Lerp(new Color(0, 200, 255), new Color(0, 255, 160), eqBars[i]);
                sb.Draw(px, new Rectangle((int)x, (int)y, (int)barW, (int)Math.Max(1f, h)),
                    new Rectangle(0, 0, 1, 1), c * alpha);
            }
        }

        private void DrawParticles(SpriteBatch sb, float alpha) {
            foreach (var node in circuitNodes)
                node.Draw(sb, alpha * 0.65f);
            foreach (var particle in dataParticles)
                particle.Draw(sb, alpha * 0.55f);
        }

        #endregion
    }
}
