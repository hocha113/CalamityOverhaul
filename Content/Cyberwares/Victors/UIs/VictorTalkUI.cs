using CalamityOverhaul.Content.Cyberwares.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
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
    /// <br/>采用贴底的赛博对话条（不遮挡主视野），复用 <see cref="EffectLoader.CyberwarePanel"/> 着色器背景，
    /// 左侧 Victor 立绘、中部打字机台词、右侧功能选项（义体诊所 / 闲聊 / 离开）
    /// </summary>
    internal class VictorTalkUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static VictorTalkUI Instance => UIHandleLoader.GetUIHandleOfType<VictorTalkUI>();

        public override bool CloseOnEscape => true;
        public override SoundStyle? OpenSound => SoundID.MenuOpen;
        public override SoundStyle? CloseSound => SoundID.MenuClose;

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
        private float revealed;//打字机已显示字符数
        private int totalChars;

        private Rectangle barRect;
        private Rectangle portraitRect;
        private Rectangle textRect;
        private Rectangle clinicBtnRect;
        private Rectangle chatBtnRect;
        private Rectangle leaveBtnRect;
        private Rectangle closeBtnRect;
        private bool clinicHover, chatHover, leaveHover, closeHover;

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
            int barW = (int)MathHelper.Clamp(Main.screenWidth - 120, 640, 1180);
            const int barH = 196;
            int x = (Main.screenWidth - barW) / 2;

            //贴底滑入
            float ease = CWRUtils.EaseOutCubic(MathHelper.Clamp(OpenProgress.Current, 0f, 1f));
            int baseY = Main.screenHeight - barH - 30;
            int y = baseY + (int)((1f - ease) * (barH + 40));
            barRect = new Rectangle(x, y, barW, barH);

            const int pad = 16;
            int portW = (int)((barH - pad * 2) * 0.72f);
            portraitRect = new Rectangle(barRect.X + pad, barRect.Y + pad, portW, barH - pad * 2);

            const int choiceW = 268;
            const int choiceH = 44;
            const int choiceGap = 10;
            int choiceX = barRect.Right - pad - choiceW;
            int choiceBlockH = choiceH * 3 + choiceGap * 2;
            int choiceY = barRect.Y + (barH - choiceBlockH) / 2;
            clinicBtnRect = new Rectangle(choiceX, choiceY, choiceW, choiceH);
            chatBtnRect = new Rectangle(choiceX, choiceY + choiceH + choiceGap, choiceW, choiceH);
            leaveBtnRect = new Rectangle(choiceX, choiceY + (choiceH + choiceGap) * 2, choiceW, choiceH);

            int textX = portraitRect.Right + 20;
            textRect = new Rectangle(textX, barRect.Y + pad, choiceX - 18 - textX, barH - pad * 2);

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

            //推进打字机
            if (!FullyRevealed) {
                revealed += 0.9f;
            }

            if (barRect.Contains(MousePoint)) {
                player.mouseInterface = true;
                //点击对话条空白处直接显示完整台词
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

            if (HandleButton(clinicBtnRect, ref clinicHover)) {
                Close();
                VictorClinicUI.Instance.Open();
                return;
            }
            if (HandleButton(chatBtnRect, ref chatHover)) {
                PickDialogue();//换一句台词
                return;
            }
            if (HandleButton(leaveBtnRect, ref leaveHover)) {
                Close();
                return;
            }
        }

        private bool HandleButton(Rectangle rect, ref bool hover) {
            bool now = rect.Contains(MousePoint);
            if (now && !hover) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
            }
            hover = now;
            if (now) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.3f });
                    return true;
                }
            }
            return false;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsOpen && OpenProgress.Current <= 0.001f) {
                return;
            }
            Layout();
            float alpha = MathHelper.Clamp(OpenProgress.Current, 0f, 1f);

            //赛博红面板着色器背景（轻量模式），其内部自行处理 End/Begin 并留下 PointClamp 批次
            CyberPanelRenderer.DrawShaderBackground(spriteBatch, alpha * 0.97f, barRect, Vector2.Zero, 0f, mode: 1);
            CyberPanelRenderer.DrawFrameDecor(spriteBatch, alpha, barRect, GlobalTimer);

            Texture2D px = CWRAsset.Placeholder_White.Value;
            DrawPortrait(spriteBatch, px, alpha);
            DrawDialogue(spriteBatch, alpha);

            DrawChoice(spriteBatch, px, clinicBtnRect, ClinicButtonText.Value, clinicHover, alpha, CyberwareTheme.Accent);
            DrawChoice(spriteBatch, px, chatBtnRect, ChatButtonText.Value, chatHover, alpha, CyberwareTheme.AccentCyan);
            DrawChoice(spriteBatch, px, leaveBtnRect, LeaveButtonText.Value, leaveHover, alpha, CyberwareTheme.TextDim);

            panelRenderer.DrawCloseButton(spriteBatch, alpha, barRect, closeHover);
        }

        private void DrawPortrait(SpriteBatch sb, Texture2D px, float alpha) {
            //立绘内嵌框
            sb.Draw(px, portraitRect, CyberwareTheme.SectionBg * (alpha * 0.92f));
            DrawRectBorder(sb, px, portraitRect, CyberwareTheme.Accent * (alpha * 0.5f), 1);

            portraitAsset ??= ModContent.Request<Texture2D>(
                "CalamityOverhaul/Content/Cyberwares/Victors/Victor", AssetRequestMode.ImmediateLoad);
            Texture2D tex = portraitAsset?.Value;
            if (tex != null) {
                int frameH = tex.Height / Victor.FrameCount;
                Rectangle src = new(0, 0, tex.Width, frameH);//站立帧
                float sc = Math.Min((portraitRect.Width - 18f) / src.Width, (portraitRect.Height - 18f) / src.Height);
                //脚部对齐底框，整体上抬一点
                Vector2 anchor = new(portraitRect.Center.X, portraitRect.Bottom - 8);
                sb.Draw(tex, anchor, src, Color.White * alpha, 0f,
                    new Vector2(src.Width / 2f, src.Height), sc, SpriteEffects.None, 0f);
            }

            //名牌
            string name = SpeakerName.Value;
            float ns = 0.5f * CyberwareTheme.FontScale;
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * ns;
            Vector2 namePos = new(portraitRect.X, portraitRect.Y - 2);
            sb.Draw(px, new Rectangle((int)namePos.X - 4, (int)namePos.Y, (int)nameSize.X + 12, (int)nameSize.Y + 2),
                CyberwareTheme.BgPanel * (alpha * 0.7f));
            sb.Draw(px, new Rectangle((int)namePos.X - 4, (int)namePos.Y, 3, (int)nameSize.Y + 2), CyberwareTheme.Accent * alpha);
            Utils.DrawBorderString(sb, name, namePos + new Vector2(4, 1), CyberwareTheme.Accent * alpha, ns);
        }

        private void DrawDialogue(SpriteBatch sb, float alpha) {
            if (string.IsNullOrEmpty(currentDialogue)) {
                totalChars = 0;
                return;
            }

            float ds = 0.46f * CyberwareTheme.FontScale;
            string[] lines = Utils.WordwrapString(currentDialogue, FontAssets.MouseText.Value,
                (int)(textRect.Width / ds), 10, out _);

            //统计总字符（用于打字机与"已完成"判定）
            int total = 0;
            foreach (string l in lines) {
                if (!string.IsNullOrEmpty(l)) {
                    total += l.Length;
                }
            }
            totalChars = total;

            int budget = (int)revealed;
            float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y * ds + 6f;
            float y = textRect.Y + 4f;
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
                y += lineHeight;
            }

            //打字机光标
            if (!FullyRevealed && (int)(GlobalTimer * 2.5f) % 2 == 0) {
                Utils.DrawBorderString(sb, "_", new Vector2(textRect.X, y - lineHeight + 2f), CyberwareTheme.Accent * alpha, ds);
            }
        }

        private static void DrawChoice(SpriteBatch sb, Texture2D px, Rectangle rect, string text, bool hover, float alpha, Color accent) {
            float hv = hover ? 1f : 0f;
            Color bg = Color.Lerp(CyberwareTheme.SlotInnerBg, accent, 0.06f + 0.22f * hv) * alpha;
            sb.Draw(px, rect, bg);
            Color border = Color.Lerp(CyberwareTheme.SlotBorder, accent, 0.25f + 0.7f * hv) * alpha;
            DrawRectBorder(sb, px, rect, border, hover ? 2 : 1);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, hover ? 4 : 2, rect.Height), accent * (alpha * (0.5f + 0.5f * hv)));

            float scale = 0.44f * CyberwareTheme.FontScale;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            Vector2 pos = new(rect.X + 18, rect.Y + (rect.Height - size.Y) / 2f);
            Color tc = Color.Lerp(CyberwareTheme.TextNormal, CyberwareTheme.TextBright, 0.4f + 0.6f * hv) * alpha;
            Utils.DrawBorderString(sb, text, pos, tc, scale);

            //右侧箭头提示
            if (hover) {
                Utils.DrawBorderString(sb, ">", new Vector2(rect.Right - 22, rect.Y + (rect.Height - size.Y) / 2f), accent * alpha, scale);
            }
        }

        private static void DrawRectBorder(SpriteBatch sb, Texture2D px, Rectangle r, Color c, int t) {
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, t), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
            sb.Draw(px, new Rectangle(r.X, r.Y, t, r.Height), c);
            sb.Draw(px, new Rectangle(r.Right - t, r.Y, t, r.Height), c);
        }
    }
}
