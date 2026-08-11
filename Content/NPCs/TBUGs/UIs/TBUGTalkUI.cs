using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// TBUG 对话控制台。布局刻意不同于维克托的居中通栏：窗口靠屏幕左下角，
    /// 立绘站在窗外、右肩压住窗左边框（她在倚着终端说话），
    /// 选项是窗底一排横向命令键而不是右侧竖排菜单
    /// </summary>
    internal class TBUGTalkUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static TBUGTalkUI Instance => UIHandleLoader.GetUIHandleOfType<TBUGTalkUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => silentClose ? null : SoundID.MenuClose;

        //切商店时静默关，免双音效叠
        private bool silentClose;

        #region 本地化

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText ShopButtonText { get; private set; }
        public static LocalizedText ChatButtonText { get; private set; }
        public static LocalizedText LeaveButtonText { get; private set; }
        public static LocalizedText PriceFactorText { get; private set; }

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "TBUG");
            ShopButtonText = this.GetLocalization(nameof(ShopButtonText), () => "Hack Shop");
            ChatButtonText = this.GetLocalization(nameof(ChatButtonText), () => "Small Talk");
            LeaveButtonText = this.GetLocalization(nameof(LeaveButtonText), () => "Leave");
            PriceFactorText = this.GetLocalization(nameof(PriceFactorText), () => "PRICE x{0}");
            //台词池统一由 TBUGDialogue 注册与分桶
            TBUGDialogue.Register(this);
        }

        #endregion

        //暂时复用 Victor 立绘（36×50 每帧）
        [VaultLoaden("CalamityOverhaul/Content/NPCs/Victors/Victor")]
        private static Asset<Texture2D> portraitAsset = null;

        //终端序号而非键位名：这些没有实际绑定按键，写 F1 会骗玩家去按
        private static readonly string[] CommandKeys = ["01", "02", "03"];

        //窗高按台词实测行数伸缩，不写死；这几段是除正文外的固定开销
        private const int HeaderBlock = 46;
        private const int CommandBlock = 36;
        private const int FooterPad = 14;
        private const int TextGap = 12;
        private const int MinConsoleHeight = 168;
        //须容得下 WrapLines 上限 8 行（46 + 8×~27 + 12 + 36 + 14 ≈ 330），钳制不能低于实排高度
        private const int MaxConsoleHeight = 360;

        private const int MarginLeft = 46;
        private const int MarginBottom = 34;
        /// <summary>立绘右肩压进窗口的像素数</summary>
        private const int PortraitOverlap = 22;

        private string currentDialogue = string.Empty;
        private List<string> wrappedLines = [];
        private float revealed;
        private int totalChars;

        //换行结果缓存：台词或窗宽没变就不重排
        private string wrapSourceText;
        private int wrapSourceWidth = -1;
        private float promptWidth;

        private Rectangle consoleRect;
        private Rectangle portraitRect;
        private Rectangle textRect;
        private Rectangle closeRect;
        private Rectangle chipRect;
        private readonly Rectangle[] cmdRects = new Rectangle[3];
        private readonly float[] cmdHover = new float[3];
        private bool closeHover;
        private bool chipHover;

        protected override void OnOpen() {
            PickDialogue();
            Array.Clear(cmdHover);
        }

        protected override void OnClose() => TBUGSession.MaybeEndSession();

        private void PickDialogue() {
            string line = TBUGDialogue.Pick();
            if (string.IsNullOrEmpty(line)) {
                return;
            }
            currentDialogue = line;
            revealed = 0f;
        }

        private bool FullyRevealed => revealed >= totalChars;

        private float LineHeight => TBUGRenderer.Measure("A", TBUGTheme.FontBody).Y + 6f;

        private void Layout() {
            float screenW = TBUGTheme.UIScreenW;
            float screenH = TBUGTheme.UIScreenH;

            int portW = 36 * TBUGTheme.PortraitScale;
            int portH = 50 * TBUGTheme.PortraitScale;

            //宽度先定，换行只依赖宽度
            int consoleX = MarginLeft + portW - PortraitOverlap;
            int consoleW = (int)MathHelper.Clamp(screenW - consoleX - 56f, 520f, 900f);

            //窗高按实测行数长出来，长台词不会被裁；宽度与 textRect 同源（左 30 右 20）
            promptWidth = TBUGRenderer.Measure(">", TBUGTheme.FontBody).X + 10f;
            int wrapWidth = consoleW - 50 - (int)promptWidth;
            if (wrapSourceText != currentDialogue || wrapSourceWidth != wrapWidth) {
                wrapSourceText = currentDialogue;
                wrapSourceWidth = wrapWidth;
                wrappedLines = TBUGRenderer.WrapLines(currentDialogue, TBUGTheme.FontBody, wrapWidth, 8);
                int total = 0;
                foreach (string l in wrappedLines) {
                    total += l.Length;
                }
                totalChars = total;
            }

            float lineH = LineHeight;
            int textH = (int)MathF.Ceiling(Math.Max(1, wrappedLines.Count) * lineH);
            int consoleH = Math.Clamp(HeaderBlock + textH + TextGap + CommandBlock + FooterPad,
                MinConsoleHeight, MaxConsoleHeight);

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            //整体自下方滑入
            int slide = (int)((1f - ease) * (consoleH + 60));
            int consoleBottom = (int)screenH - MarginBottom + slide;
            consoleRect = new Rectangle(consoleX, consoleBottom - consoleH, consoleW, consoleH);

            //立绘脚底与窗底齐平，头顶越过窗顶
            portraitRect = new Rectangle(MarginLeft, consoleBottom - portH, portW, portH);

            //底部命令栏；内容左内边距 30，给压边立绘的右缘让出空隙
            int cmdY = consoleRect.Bottom - CommandBlock - FooterPad;
            string[] keys = CommandKeys;
            string[] labels = [ShopButtonText.Value, ChatButtonText.Value, LeaveButtonText.Value];
            int x = consoleRect.X + 30;
            for (int i = 0; i < 3; i++) {
                int w = TBUGRenderer.MeasureCommandButton(keys[i], labels[i]);
                cmdRects[i] = new Rectangle(x, cmdY, w, CommandBlock);
                x += w + 12;
            }

            //正文区：标题栏之下、命令栏之上
            int textTop = consoleRect.Y + HeaderBlock;
            textRect = new Rectangle(consoleRect.X + 30, textTop,
                consoleRect.Width - 50, cmdY - TextGap - textTop);

            closeRect = TBUGRenderer.GetCloseRect(consoleRect);

            //价格系数徽标，悬停弹幸福度报告
            string chip = PriceFactorText.Format(TBUGMood.PriceAdjustment.ToString("0.00"));
            Vector2 chipSize = TBUGRenderer.Measure(chip, TBUGTheme.FontLabel);
            chipRect = new Rectangle((int)(closeRect.X - 16f - chipSize.X), consoleRect.Y + 13,
                (int)chipSize.X + 6, (int)chipSize.Y + 4);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            if (!IsOpen) {
                return;
            }

            //绑定的 TBUG 没了（被杀/消失）就收窗，别让玩家对着空气聊天
            if (!TBUGSession.IsBoundNPCAlive()) {
                Close();
                return;
            }

            if (!FullyRevealed) {
                revealed += 1.1f;
            }

            if (consoleRect.Contains(MousePoint) || portraitRect.Contains(MousePoint)) {
                player.mouseInterface = true;
            }

            //点正文区跳过打字机
            if (keyLeftPressState == KeyPressState.Pressed && textRect.Contains(MousePoint) && !FullyRevealed) {
                revealed = totalChars;
                return;
            }

            chipHover = chipRect.Contains(MousePoint);
            closeHover = closeRect.Contains(MousePoint);
            if (closeHover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Close();
                    return;
                }
            }

            for (int i = 0; i < 3; i++) {
                bool now = cmdRects[i].Contains(MousePoint);
                if (now && cmdHover[i] < 0.01f) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
                }
                if (now) {
                    player.mouseInterface = true;
                }
                cmdHover[i] = MathHelper.Clamp(cmdHover[i] + (now ? 0.2f : -0.2f), 0f, 1f);

                if (now && keyLeftPressState == KeyPressState.Pressed) {
                    Invoke(i);
                    return;
                }
            }
        }

        private void Invoke(int index) {
            switch (index) {
                case 0:
                    //静默关对话，只留商店 OpenSound；关闭回调会清会话，先存再重绑
                    int who = TBUGSession.BoundWhoAmI;
                    silentClose = true;
                    Close();
                    silentClose = false;
                    TBUGSession.Bind(who);
                    TBUGShopUI.Instance.Open();
                    break;
                case 1:
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
                    PickDialogue();
                    break;
                default:
                    Close();
                    break;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);

            TBUGRenderer.DrawDropShadow(spriteBatch, consoleRect, alpha);
            TBUGRenderer.DrawGlassPanel(spriteBatch, consoleRect, alpha);
            TBUGRenderer.DrawScanSweep(spriteBatch, consoleRect, alpha, GlobalTimer);
            TBUGRenderer.DrawChamferFrame(spriteBatch, consoleRect,
                TBUGTheme.Blue * (alpha * 0.75f), 1.6f, TBUGTheme.Chamfer, glow: true);

            DrawHeader(spriteBatch, alpha);
            DrawDialogue(spriteBatch, alpha);

            string[] keys = CommandKeys;
            string[] labels = [ShopButtonText.Value, ChatButtonText.Value, LeaveButtonText.Value];
            Color[] accents = [TBUGTheme.Blue, TBUGTheme.Blue, TBUGTheme.Danger];
            for (int i = 0; i < 3; i++) {
                TBUGRenderer.DrawCommandButton(spriteBatch, cmdRects[i], keys[i], labels[i],
                    cmdHover[i], alpha, accents[i]);
            }

            TBUGRenderer.DrawClose(spriteBatch, consoleRect, alpha, closeHover);
            //立绘最后画，右肩压住窗左边框
            DrawPortrait(spriteBatch, alpha);
            DrawMoodTip(spriteBatch, alpha);
        }

        /// <summary>悬停价格系数徽标时弹幸福度报告，解释这个系数是怎么来的</summary>
        private void DrawMoodTip(SpriteBatch sb, float alpha) {
            //关闭淡出期间悬停态是残值，别让介绍框跟着鼠标飘
            if (!IsOpen || !chipHover) {
                return;
            }
            string report = TBUGMood.Report;
            if (string.IsNullOrWhiteSpace(report)) {
                return;
            }
            double factor = TBUGMood.PriceAdjustment;
            Color tone = factor < 0.995 ? TBUGTheme.Blue
                : factor > 1.005 ? TBUGTheme.Danger : TBUGTheme.Ice;
            TBUGRenderer.DrawCursorPanel(sb, MousePoint.ToVector2(), alpha,
                SpeakerName.Value, tone,
                TBUGRenderer.WrapLines(report, TBUGTheme.FontBody, 380f, 8),
                null, default, 0L, tone,
                PriceFactorText.Format(factor.ToString("0.00")));
        }

        private void DrawHeader(SpriteBatch sb, float alpha) {
            float y = consoleRect.Y + 12f;
            float x = consoleRect.X + 30f;

            TBUGRenderer.DrawGlowText(sb, "▸", new Vector2(x, y),
                TBUGTheme.Blue * alpha, TBUGTheme.Blue * (alpha * 0.3f), TBUGTheme.FontTitle);
            x += TBUGRenderer.Measure("▸", TBUGTheme.FontTitle).X + 8f;

            string name = SpeakerName.Value;
            TBUGRenderer.DrawGlowText(sb, name, new Vector2(x, y),
                TBUGTheme.Ice * alpha, TBUGTheme.Blue * (alpha * 0.35f), TBUGTheme.FontTitle);

            //右侧价格系数徽标：便宜蓝、正常暗、贵报错红；位置由 chipRect 统一给
            double factor = TBUGMood.PriceAdjustment;
            string chip = PriceFactorText.Format(factor.ToString("0.00"));
            Color chipColor = factor < 0.995 ? TBUGTheme.Blue
                : factor > 1.005 ? TBUGTheme.Danger : TBUGTheme.TextDim;
            if (chipHover) {
                chipColor = TBUGTheme.Ice;
            }
            TBUGRenderer.DrawText(sb, chip, new Vector2(chipRect.X, chipRect.Y),
                chipColor * alpha, TBUGTheme.FontLabel);

            TBUGRenderer.DrawRule(sb, consoleRect.X + 14, consoleRect.Right - 14, consoleRect.Y + 38,
                TBUGTheme.Line * alpha, TBUGTheme.Blue * (alpha * 0.55f));
        }

        private void DrawDialogue(SpriteBatch sb, float alpha) {
            if (wrappedLines.Count == 0) {
                return;
            }

            //提示符占一格缩进，正文从其右侧起排；换行结果由 Layout 缓存
            float lineH = LineHeight;
            float y = textRect.Y;
            TBUGRenderer.DrawText(sb, ">", new Vector2(textRect.X, y), TBUGTheme.Blue * alpha, TBUGTheme.FontBody);

            int budget = (int)revealed;
            float lastY = y;
            float lastX = textRect.X + promptWidth;
            foreach (string line in wrappedLines) {
                //防御：窗高被 Max 钳住的极端情况下不许画进命令栏
                if (budget <= 0 || y + lineH > textRect.Bottom + 4f) {
                    break;
                }
                int take = Math.Min(budget, line.Length);
                string seg = take >= line.Length ? line : line[..take];
                TBUGRenderer.DrawText(sb, seg, new Vector2(textRect.X + promptWidth, y),
                    TBUGTheme.Text * alpha, TBUGTheme.FontBody);
                lastX = textRect.X + promptWidth + TBUGRenderer.Measure(seg, TBUGTheme.FontBody).X;
                lastY = y;
                budget -= line.Length;
                y += lineH;
            }

            //未打完时光标跟在最后一个字后面
            if (!FullyRevealed && (int)(GlobalTimer * 3f) % 2 == 0) {
                sb.Draw(TBUGRenderer.Pixel, new Rectangle((int)lastX + 2, (int)lastY + 4, 8, 16),
                    new Rectangle(0, 0, 1, 1), TBUGTheme.Blue * (alpha * 0.85f));
            }
        }

        private void DrawPortrait(SpriteBatch sb, float alpha) {
            Texture2D tex = portraitAsset?.Value;
            if (tex == null) {
                return;
            }
            int frameH = tex.Height / TBUG.FrameCount;
            Rectangle src = new(0, 0, tex.Width, frameH);

            //整数倍放大保持像素清晰；贴图默认朝左，翻转朝向右侧的终端
            Vector2 anchor = new(portraitRect.Center.X, portraitRect.Bottom);
            sb.Draw(tex, anchor, src, Color.White * alpha, 0f,
                new Vector2(src.Width / 2f, src.Height), TBUGTheme.PortraitScale,
                SpriteEffects.FlipHorizontally, 0f);
        }
    }
}
