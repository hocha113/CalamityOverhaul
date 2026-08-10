using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>TBUG 对话条，右键开；终端窗口 + 立绘打字机 + 三按钮</summary>
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

        //暂时复用 Victor 立绘
        [VaultLoaden("CalamityOverhaul/Content/NPCs/Victors/Victor")]
        private static Asset<Texture2D> portraitAsset = null;

        private string currentDialogue = string.Empty;
        private float revealed;
        private int totalChars;

        private Rectangle barRect;
        private Rectangle portraitRect;
        private Rectangle textRect;
        private Rectangle shopBtnRect;
        private Rectangle chatBtnRect;
        private Rectangle leaveBtnRect;
        private Rectangle closeBtnRect;
        private bool shopHover, chatHover, leaveHover, closeHover;
        private float shopT, chatT, leaveT;

        private readonly TBUGPanelRenderer panelRenderer = new();

        protected override void OnOpen() => PickDialogue();

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

        private void Layout() {
            //UIHandle 跑在 UIScale 空间，屏尺寸走主题访问器
            int screenW = (int)TBUGTheme.UIScreenW;
            int screenH = (int)TBUGTheme.UIScreenH;
            int barW = (int)MathHelper.Clamp(screenW - 120, 760, 1240);
            const int barH = 214;
            int x = (screenW - barW) / 2;

            float ease = VaultUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int baseY = screenH - barH - 28;
            int y = baseY + (int)((1f - ease) * (barH + 44));
            barRect = new Rectangle(x, y, barW, barH);

            const int pad = 18;
            int portH = barH - pad * 2;
            int portW = (int)(portH * 0.74f);
            portraitRect = new Rectangle(barRect.X + pad, barRect.Y + pad, portW, portH);

            const int choiceW = 330;
            int choiceX = barRect.Right - pad - choiceW;
            const int rowH = 58;
            int blockH = rowH * 3;
            int rowY = barRect.Y + (barH - blockH) / 2;
            shopBtnRect = new Rectangle(choiceX, rowY, choiceW, rowH);
            chatBtnRect = new Rectangle(choiceX, rowY + rowH, choiceW, rowH);
            leaveBtnRect = new Rectangle(choiceX, rowY + rowH * 2, choiceW, rowH);

            int textX = portraitRect.Right + 28;
            textRect = new Rectangle(textX, barRect.Y + pad, choiceX - 18 - textX, portH);

            closeBtnRect = TBUGPanelRenderer.GetCloseButtonRect(barRect);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            panelRenderer.Update();
            if (!IsOpen) {
                return;
            }

            if (!FullyRevealed) {
                revealed += 0.9f;
            }

            if (barRect.Contains(MousePoint)) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed && !FullyRevealed
                    && !shopBtnRect.Contains(MousePoint) && !chatBtnRect.Contains(MousePoint)
                    && !leaveBtnRect.Contains(MousePoint) && !closeBtnRect.Contains(MousePoint)) {
                    revealed = totalChars;
                }
            }

            closeHover = closeBtnRect.Contains(MousePoint);
            if (closeHover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Close();
                    return;
                }
            }

            UpdateHover(shopBtnRect, ref shopHover, ref shopT);
            UpdateHover(chatBtnRect, ref chatHover, ref chatT);
            UpdateHover(leaveBtnRect, ref leaveHover, ref leaveT);

            if (shopHover && keyLeftPressState == KeyPressState.Pressed) {
                //静默关对话，只留商店 OpenSound；关闭回调会清会话，先存再重绑
                int who = TBUGSession.BoundWhoAmI;
                silentClose = true;
                Close();
                silentClose = false;
                TBUGSession.Bind(who);
                TBUGShopUI.Instance.Open();
                return;
            }
            if (chatHover && keyLeftPressState == KeyPressState.Pressed) {
                Click();
                PickDialogue();
                return;
            }
            if (leaveHover && keyLeftPressState == KeyPressState.Pressed) {
                //CloseSound 播关闭音
                Close();
                return;
            }
        }

        private void UpdateHover(Rectangle rect, ref bool hover, ref float t) {
            bool now = rect.Contains(MousePoint);
            if (now && !hover) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
            }
            hover = now;
            if (now) {
                player.mouseInterface = true;
            }
            t = MathHelper.Clamp(t + (now ? 0.18f : -0.18f), 0f, 1f);
        }

        private static void Click() => SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);

            TBUGPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, barRect);
            TBUGPanelRenderer.DrawFrameDecor(spriteBatch, alpha, barRect, GlobalTimer);

            Texture2D px = VaultAsset.placeholder2.Value;

            DrawPortrait(spriteBatch, alpha);
            TBUGUIStyle.DrawVDivider(spriteBatch, portraitRect.Right + 13, barRect.Y + 14, barRect.Bottom - 14, TBUGTheme.Accent * (alpha * 0.5f));
            DrawTextBlock(spriteBatch, px, alpha);
            TBUGUIStyle.DrawVDivider(spriteBatch, shopBtnRect.X - 14, barRect.Y + 14, barRect.Bottom - 14, TBUGTheme.Accent * (alpha * 0.4f));

            DrawChoiceRow(spriteBatch, shopBtnRect, ShopButtonText.Value, shopT, alpha, TBUGTheme.Accent);
            DrawChoiceRow(spriteBatch, chatBtnRect, ChatButtonText.Value, chatT, alpha, TBUGTheme.AccentAmber);
            DrawChoiceRow(spriteBatch, leaveBtnRect, LeaveButtonText.Value, leaveT, alpha, TBUGTheme.AccentErr);

            panelRenderer.DrawGlitchEffect(spriteBatch, alpha, barRect);
            panelRenderer.DrawCloseButton(spriteBatch, alpha, barRect, closeHover);
        }

        private void DrawPortrait(SpriteBatch sb, float alpha) {
            TBUGUIStyle.DrawHoloFrame(sb, portraitRect, TBUGTheme.Accent, alpha, GlobalTimer);
            Texture2D tex = portraitAsset?.Value;
            if (tex != null) {
                int frameH = tex.Height / TBUG.FrameCount;
                Rectangle src = new(0, 0, tex.Width, frameH);
                float sc = Math.Min((portraitRect.Width - 20f) / src.Width, (portraitRect.Height - 22f) / src.Height);
                Vector2 anchor = new(portraitRect.Center.X, portraitRect.Bottom - 10);
                sb.Draw(tex, anchor, src, Color.White * alpha, 0f, new Vector2(src.Width / 2f, src.Height), sc, SpriteEffects.FlipHorizontally, 0f);
            }
        }

        private void DrawTextBlock(SpriteBatch sb, Texture2D px, float alpha) {
            string name = SpeakerName.Value;
            float nameScale = 0.82f * TBUGTheme.FontScale;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * nameScale;
            float nameY = textRect.Y + 2f;
            sb.Draw(px, new Rectangle(textRect.X, (int)nameY + 3, 4, (int)nameSize.Y - 4), TBUGTheme.Accent * alpha);
            Utils.DrawBorderString(sb, name, new Vector2(textRect.X + 12, nameY), TBUGTheme.Accent * alpha, nameScale);

            //名字行右侧：幸福度价格系数徽标
            double factor = TBUGMood.PriceAdjustment;
            string factorText = PriceFactorText.Format(factor.ToString("0.00"));
            float factorScale = 0.5f * TBUGTheme.FontScale;
            Vector2 factorSize = FontAssets.MouseText.Value.MeasureString(factorText) * factorScale;
            Color factorColor = factor < 0.995 ? TBUGTheme.Accent
                : factor > 1.005 ? TBUGTheme.AccentErr : TBUGTheme.TextDim;
            Utils.DrawBorderString(sb, factorText,
                new Vector2(textRect.Right - factorSize.X, nameY + (nameSize.Y - factorSize.Y) * 0.5f),
                factorColor * alpha, factorScale);

            int divY = (int)(nameY + nameSize.Y + 6f);
            TBUGUIStyle.DrawHDivider(sb, textRect.X, textRect.Right, divY, TBUGTheme.Accent * (alpha * 0.5f));

            //底部心情条：报告最多两行，台词区在其上方截断
            float moodTop = textRect.Bottom;
            string report = TBUGMood.Report;
            if (!string.IsNullOrEmpty(report)) {
                float moodScale = 0.5f * TBUGTheme.FontScale;
                string[] moodLines = VaultUtils.WrapTextArray(report, FontAssets.MouseText.Value, (int)(textRect.Width / moodScale), 2, out _);
                float moodLineH = FontAssets.MouseText.Value.MeasureString("A").Y * moodScale + 4f;
                int moodCount = 0;
                foreach (string l in moodLines) {
                    if (!string.IsNullOrEmpty(l)) {
                        moodCount++;
                    }
                }
                if (moodCount > 0) {
                    moodTop = textRect.Bottom - moodCount * moodLineH - 6f;
                    TBUGUIStyle.DrawHDivider(sb, textRect.X, textRect.Right, (int)moodTop, TBUGTheme.Accent * (alpha * 0.25f));
                    float moodY = moodTop + 5f;
                    foreach (string line in moodLines) {
                        if (string.IsNullOrEmpty(line)) {
                            continue;
                        }
                        Utils.DrawBorderString(sb, line, new Vector2(textRect.X, moodY), TBUGTheme.TextDim * (alpha * 0.85f), moodScale);
                        moodY += moodLineH;
                    }
                }
            }

            if (string.IsNullOrEmpty(currentDialogue)) {
                totalChars = 0;
                return;
            }

            float ds = 0.8f * TBUGTheme.FontScale;
            string[] lines = VaultUtils.WrapTextArray(currentDialogue, FontAssets.MouseText.Value, (int)(textRect.Width / ds), 8, out _);
            int total = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrEmpty(l)) {
                    total += l.Length;
                }
            }
            totalChars = total;

            float lineH = FontAssets.MouseText.Value.MeasureString("A").Y * ds + 7f;
            float areaTop = divY + 12f;
            float y = areaTop;

            int budget = (int)revealed;
            float lastY = y;
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                if (budget <= 0) {
                    break;
                }
                //不侵入底部心情条
                if (y + lineH > moodTop - 3f) {
                    break;
                }
                int take = Math.Min(budget, line.Length);
                string seg = take >= line.Length ? line : line[..take];
                Utils.DrawBorderString(sb, seg, new Vector2(textRect.X, y), TBUGTheme.TextBright * alpha, ds);
                budget -= line.Length;
                lastY = y;
                y += lineH;
            }

            if (!FullyRevealed && (int)(GlobalTimer * 2.5f) % 2 == 0) {
                Utils.DrawBorderString(sb, "▌", new Vector2(textRect.X + 2, lastY + lineH * 0.05f), TBUGTheme.Accent * alpha, ds);
            }
        }

        private static void DrawChoiceRow(SpriteBatch sb, Rectangle rect, string text, float hoverT, float alpha, Color accent) {
            int slide = TBUGUIStyle.DrawCommandRow(sb, rect, accent, hoverT, alpha);
            float scale = 0.6f * TBUGTheme.FontScale;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            float ty = rect.Y + (rect.Height - size.Y) / 2f;
            Color tc = Color.Lerp(TBUGTheme.TextNormal, TBUGTheme.TextBright, 0.45f + 0.55f * hoverT) * alpha;
            Utils.DrawBorderString(sb, text, new Vector2(rect.X + 20 + slide, ty), tc, scale);
            Utils.DrawBorderString(sb, ">", new Vector2(rect.Right - 26 - hoverT * 4f, ty), accent * (alpha * (0.35f + 0.65f * hoverT)), scale);
        }
    }
}
