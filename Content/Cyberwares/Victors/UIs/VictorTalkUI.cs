using CalamityOverhaul.Content.Cyberwares.UIs;
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

namespace CalamityOverhaul.Content.Cyberwares.Victors.UIs
{
    /// <summary>
    /// Victor 的交流界面：右键 Victor 打开。
    /// <br/>贴底赛博对话条（不遮挡主视野），复用 <see cref="EffectLoader.CyberwarePanel"/> 着色器背景，
    /// 左侧全息立绘、中部姓名 + 打字机台词、右侧命令行式功能选项（义体诊所 / 闲聊 / 离开）
    /// </summary>
    internal class VictorTalkUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static VictorTalkUI Instance => UIHandleLoader.GetUIHandleOfType<VictorTalkUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => silentClose ? null : SoundID.MenuClose;

        //切换到义体诊所时静默关闭，避免关闭音与诊所开启音重叠
        private bool silentClose;

        #region 本地化

        public static LocalizedText SpeakerName { get; private set; }
        public static LocalizedText ClinicButtonText { get; private set; }
        public static LocalizedText ChatButtonText { get; private set; }
        public static LocalizedText LeaveButtonText { get; private set; }
        private static LocalizedText[] greetings;

        public override void SetStaticDefaults() {
            SpeakerName = this.GetLocalization(nameof(SpeakerName), () => "VICTOR");
            ClinicButtonText = this.GetLocalization(nameof(ClinicButtonText), () => "Cyberware Clinic");
            ChatButtonText = this.GetLocalization(nameof(ChatButtonText), () => "Small Talk");
            LeaveButtonText = this.GetLocalization(nameof(LeaveButtonText), () => "Leave");
            greetings = [
                this.GetLocalization("Greet0", () => "Another customer? Come in, before the draft scatters your spare parts."),
                this.GetLocalization("Greet1", () => "Want a stronger body? Steel never betrays you - only your wallet does."),
                this.GetLocalization("Greet2", () => "Brain, eyes, limbs - if the price is right, there is nothing I cannot replace."),
                this.GetLocalization("Greet3", () => "Sit on the table. Let me see what flesh of yours is still worth keeping."),
            ];
        }

        #endregion

        private static Asset<Texture2D> portraitAsset;

        private string currentDialogue = string.Empty;
        private float revealed;
        private int totalChars;

        private Rectangle barRect;
        private Rectangle portraitRect;
        private Rectangle textRect;
        private Rectangle clinicBtnRect;
        private Rectangle chatBtnRect;
        private Rectangle leaveBtnRect;
        private Rectangle closeBtnRect;
        private bool clinicHover, chatHover, leaveHover, closeHover;
        private float clinicT, chatT, leaveT;

        private readonly CyberPanelRenderer panelRenderer = new();

        protected override void OnOpen() => PickDialogue();

        private void PickDialogue() {
            if (greetings != null && greetings.Length > 0) {
                currentDialogue = greetings[Main.rand.Next(greetings.Length)].Value;
            }
            revealed = 0f;
        }

        private bool FullyRevealed => revealed >= totalChars;

        private void Layout() {
            int barW = (int)MathHelper.Clamp(Main.screenWidth - 120, 760, 1240);
            const int barH = 214;
            int x = (Main.screenWidth - barW) / 2;

            float ease = CWRUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int baseY = Main.screenHeight - barH - 28;
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
            clinicBtnRect = new Rectangle(choiceX, rowY, choiceW, rowH);
            chatBtnRect = new Rectangle(choiceX, rowY + rowH, choiceW, rowH);
            leaveBtnRect = new Rectangle(choiceX, rowY + rowH * 2, choiceW, rowH);

            int textX = portraitRect.Right + 28;
            textRect = new Rectangle(textX, barRect.Y + pad, choiceX - 18 - textX, portH);

            closeBtnRect = CyberPanelRenderer.GetCloseButtonRect(barRect);
        }

        public override void Update() {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            if (!IsOpen) {
                return;
            }

            if (!FullyRevealed) {
                revealed += 0.9f;
            }

            if (barRect.Contains(MousePoint)) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed && !FullyRevealed
                    && !clinicBtnRect.Contains(MousePoint) && !chatBtnRect.Contains(MousePoint)
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

            UpdateHover(clinicBtnRect, ref clinicHover, ref clinicT);
            UpdateHover(chatBtnRect, ref chatHover, ref chatT);
            UpdateHover(leaveBtnRect, ref leaveHover, ref leaveT);

            if (clinicHover && keyLeftPressState == KeyPressState.Pressed) {
                //静默关闭对话框，只保留诊所开启音，避免两音重叠
                silentClose = true;
                Close();
                silentClose = false;
                VictorClinicUI.Instance.Open();
                return;
            }
            if (chatHover && keyLeftPressState == KeyPressState.Pressed) {
                Click();
                PickDialogue();
                return;
            }
            if (leaveHover && keyLeftPressState == KeyPressState.Pressed) {
                //仅播放关闭音（由 CloseSound 触发）
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

            CyberPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, barRect, Vector2.Zero, 0f, mode: 1);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, alpha, barRect, GlobalTimer);

            Texture2D px = CWRAsset.Placeholder_White.Value;

            DrawPortrait(spriteBatch, px, alpha);
            VictorUIStyle.DrawVDivider(spriteBatch, portraitRect.Right + 13, barRect.Y + 14, barRect.Bottom - 14, CyberwareTheme.Accent * (alpha * 0.5f));
            DrawTextBlock(spriteBatch, px, alpha);
            VictorUIStyle.DrawVDivider(spriteBatch, clinicBtnRect.X - 14, barRect.Y + 14, barRect.Bottom - 14, CyberwareTheme.Accent * (alpha * 0.4f));

            DrawChoiceRow(spriteBatch, clinicBtnRect, ClinicButtonText.Value, clinicT, alpha, CyberwareTheme.Accent);
            DrawChoiceRow(spriteBatch, chatBtnRect, ChatButtonText.Value, chatT, alpha, CyberwareTheme.AccentCyan);
            DrawChoiceRow(spriteBatch, leaveBtnRect, LeaveButtonText.Value, leaveT, alpha, CyberwareTheme.AccentGold);

            panelRenderer.DrawCloseButton(spriteBatch, alpha, barRect, closeHover);
        }

        private void DrawPortrait(SpriteBatch sb, Texture2D px, float alpha) {
            VictorUIStyle.DrawHoloFrame(sb, portraitRect, CyberwareTheme.Accent, alpha, GlobalTimer);

            portraitAsset ??= ModContent.Request<Texture2D>(
                "CalamityOverhaul/Content/Cyberwares/Victors/Victor", AssetRequestMode.ImmediateLoad);
            Texture2D tex = portraitAsset?.Value;
            if (tex != null) {
                int frameH = tex.Height / Victor.FrameCount;
                Rectangle src = new(0, 0, tex.Width, frameH);
                float sc = Math.Min((portraitRect.Width - 20f) / src.Width, (portraitRect.Height - 22f) / src.Height);
                Vector2 anchor = new(portraitRect.Center.X, portraitRect.Bottom - 10);
                sb.Draw(tex, anchor, src, Color.White * alpha, 0f, new Vector2(src.Width / 2f, src.Height), sc, SpriteEffects.None, 0f);
            }
        }

        private void DrawTextBlock(SpriteBatch sb, Texture2D px, float alpha) {
            //姓名抬头
            string name = SpeakerName.Value;
            float nameScale = 0.82f * CyberwareTheme.FontScale;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * nameScale;
            float nameY = textRect.Y + 2f;
            sb.Draw(px, new Rectangle(textRect.X, (int)nameY + 3, 4, (int)nameSize.Y - 4), CyberwareTheme.Accent * alpha);
            Utils.DrawBorderString(sb, name, new Vector2(textRect.X + 12, nameY), CyberwareTheme.Accent * alpha, nameScale);
            int divY = (int)(nameY + nameSize.Y + 6f);
            VictorUIStyle.DrawHDivider(sb, textRect.X, textRect.Right, divY, CyberwareTheme.Accent * (alpha * 0.5f));

            if (string.IsNullOrEmpty(currentDialogue)) {
                totalChars = 0;
                return;
            }

            float ds = 0.62f * CyberwareTheme.FontScale;
            string[] lines = Utils.WordwrapString(currentDialogue, FontAssets.MouseText.Value, (int)(textRect.Width / ds), 8, out _);
            int total = 0;
            int lineCount = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrEmpty(l)) {
                    total += l.Length;
                    lineCount++;
                }
            }
            totalChars = total;

            float lineH = FontAssets.MouseText.Value.MeasureString("A").Y * ds + 7f;
            float areaTop = divY + 12f;
            float blockH = lineCount * lineH;
            float y = areaTop + Math.Max(0f, (textRect.Bottom - areaTop - blockH) / 2f);

            int budget = (int)revealed;
            float lastY = y;
            foreach (string line in lines) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                if (budget <= 0) {
                    break;
                }
                int take = Math.Min(budget, line.Length);
                string seg = take >= line.Length ? line : line[..take];
                Utils.DrawBorderString(sb, seg, new Vector2(textRect.X, y), CyberwareTheme.TextBright * alpha, ds);
                budget -= line.Length;
                lastY = y;
                y += lineH;
            }

            if (!FullyRevealed && (int)(GlobalTimer * 2.5f) % 2 == 0) {
                Utils.DrawBorderString(sb, "▌", new Vector2(textRect.X + 2, lastY + lineH * 0.05f), CyberwareTheme.Accent * alpha, ds);
            }
        }

        private static void DrawChoiceRow(SpriteBatch sb, Rectangle rect, string text, float hoverT, float alpha, Color accent) {
            int slide = VictorUIStyle.DrawCommandRow(sb, rect, accent, hoverT, alpha);
            float scale = 0.6f * CyberwareTheme.FontScale;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            float ty = rect.Y + (rect.Height - size.Y) / 2f;
            Color tc = Color.Lerp(CyberwareTheme.TextNormal, CyberwareTheme.TextBright, 0.45f + 0.55f * hoverT) * alpha;
            Utils.DrawBorderString(sb, text, new Vector2(rect.X + 20 + slide, ty), tc, scale);
            Utils.DrawBorderString(sb, ">", new Vector2(rect.Right - 26 - hoverT * 4f, ty), accent * (alpha * (0.35f + 0.65f * hoverT)), scale);
        }
    }
}
