using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>通关面板，RETRY/EXIT，EntrustGuideCard青色</summary>
    internal class CybCourseCompletePanel : ModSystem, ILocalizedModType
    {
        private enum Phase { Hidden, FadeIn, Idle, FadeOut }

        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText Title { get; private set; }
        public static LocalizedText Subtitle { get; private set; }
        public static LocalizedText Stat1 { get; private set; }
        public static LocalizedText Stat2 { get; private set; }
        public static LocalizedText Stat3 { get; private set; }
        public static LocalizedText BtnRetry { get; private set; }
        public static LocalizedText BtnExit { get; private set; }
        public static LocalizedText Footer { get; private set; }

        public override void SetStaticDefaults() {
            Title = this.GetLocalization(nameof(Title), () => "TRAINING COMPLETE");
            Subtitle = this.GetLocalization(nameof(Subtitle), () => "SUPERDREAM PROTOCOL");
            Stat1 = this.GetLocalization(nameof(Stat1), () => "[#] SHPC HUD 校准完毕");
            Stat2 = this.GetLocalization(nameof(Stat2), () => "[#] 骇客时间校准完毕");
            Stat3 = this.GetLocalization(nameof(Stat3), () => "[#] 物块扫描接口校准完毕");
            BtnRetry = this.GetLocalization(nameof(BtnRetry), () => "RETRY");
            BtnExit = this.GetLocalization(nameof(BtnExit), () => "EXIT");
            Footer = this.GetLocalization(nameof(Footer), () => "选择以继续 — RETRY 重启训练，EXIT 离开超梦");
        }

        private const int PanelW = 460;
        private const int PanelH = 280;
        private const int EdgePad = 10;

        public static bool Visible => _phase != Phase.Hidden;

        private static Phase _phase = Phase.Hidden;
        private static float _alpha = 0f;
        private static float _shaderTimer = 0f;
        private static float _idleTimer = 0f;
        private static bool _prevMouseLeft = false;
        private static Rectangle _retryRect = Rectangle.Empty;
        private static Rectangle _exitRect = Rectangle.Empty;
        private static Rectangle _panelRect = Rectangle.Empty;

        public override void OnWorldUnload() => Hide();

        public static void Show() {
            _phase = Phase.FadeIn;
            _alpha = 0f;
            _idleTimer = 0f;
        }

        public static void Hide() {
            _phase = Phase.Hidden;
            _alpha = 0f;
            _idleTimer = 0f;
            _retryRect = Rectangle.Empty;
            _exitRect = Rectangle.Empty;
            _panelRect = Rectangle.Empty;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!CybCourseWorld.Active) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shaderTimer += dt * 0.8f;
            if (_shaderTimer > 100f) _shaderTimer -= 100f;

            switch (_phase) {
                case Phase.FadeIn:
                    _alpha = MathHelper.Lerp(_alpha, 1f, 0.14f);
                    if (_alpha > 0.985f) {
                        _alpha = 1f;
                        _phase = Phase.Idle;
                    }
                    break;
                case Phase.Idle:
                    _idleTimer += dt;
                    HandleClicks();
                    break;
                case Phase.FadeOut:
                    _alpha = MathHelper.Lerp(_alpha, 0f, 0.18f);
                    if (_alpha < 0.02f) {
                        Hide();
                    }
                    break;
            }

            //面板开时mouseInterface
            if (_panelRect != Rectangle.Empty && _phase != Phase.Hidden) {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private static void HandleClicks() {
            bool mouseDown = Main.mouseLeft;
            bool clicked = mouseDown && !_prevMouseLeft;
            _prevMouseLeft = mouseDown;
            if (!clicked) return;

            int mx = Main.mouseX;
            int my = Main.mouseY;
            if (_retryRect.Contains(mx, my)) {
                Main.mouseLeft = false;
                CybCourse.Restart();
                return;
            }
            if (_exitRect.Contains(mx, my)) {
                Main.mouseLeft = false;
                //Exit走KeyBind提醒
                Hide();
                CybCourseKeyBindReminderPanel.ShowOrExit();
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase == Phase.Hidden) return;
            if (_alpha < 0.01f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: CybCourse Complete Panel",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(0, 4, 10, (int)(140 * _alpha)));

            int cx = (Main.screenWidth - PanelW) / 2;
            int cy = (Main.screenHeight - PanelH) / 2;
            float slideY = (1f - _alpha) * 24f;
            int finalY = (int)MathHelper.Clamp(cy + (int)slideY, 8, Math.Max(8, Main.screenHeight - PanelH - 8));
            int finalX = (int)MathHelper.Clamp(cx, 8, Math.Max(8, Main.screenWidth - PanelW - 8));
            var panel = new Rectangle(finalX, finalY, PanelW, PanelH);
            _panelRect = panel;

            DrawPanelBg(sb, panel);
            DrawPanelContent(sb, panel);
        }

        private static void DrawPanelBg(SpriteBatch sb, Rectangle panel) {
            Effect effect = EffectLoader.EntrustGuideCard?.Value;
            if (effect != null) {
                Rectangle ext = panel;
                ext.Inflate(EdgePad, EdgePad);
                effect.Parameters["uTime"]?.SetValue(_shaderTimer);
                effect.Parameters["uAlpha"]?.SetValue(_alpha * 0.97f);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
                effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
                effect.Parameters["uVariant"]?.SetValue(1f);
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                sb.Draw(VaultAsset.placeholder2.Value, ext, Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.AnisotropicClamp, DepthStencilState.None,
                    RasterizerState.CullNone, null, Main.UIScaleMatrix);
            }
            else {
                sb.Draw(VaultAsset.placeholder2.Value, panel, new Color(0, 8, 18, (int)(220 * _alpha)));
                BaseManagerStyle.StrokeRect(sb, panel, 1, new Color(50, 160, 200, (int)(160 * _alpha)));
            }
        }

        private static void DrawPanelContent(SpriteBatch sb) => DrawPanelContent(sb, _panelRect);

        private static void DrawPanelContent(SpriteBatch sb, Rectangle panel) {
            var font = FontAssets.MouseText.Value;
            float titleSc = 1.30f;
            float subSc = 0.62f;
            float bodySc = 0.74f;
            float footerSc = 0.55f;

            float breath = 0.55f + 0.45f * MathF.Sin(_shaderTimer * 4f);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.X + 14, panel.Y + 8, panel.Width - 28, 2),
                new Color(80, 220, 245, (int)(140 * _alpha * breath)));

            float titleY = panel.Y + 22f;
            BaseManagerStyle.DrawCenteredText(sb, Title.Value,
                new Vector2(panel.Center.X, titleY + font.MeasureString("A").Y * titleSc * 0.5f),
                new Color(80, 230, 250, (int)(255 * _alpha)), titleSc);

            float subY = titleY + font.MeasureString("A").Y * titleSc + 6f;
            BaseManagerStyle.DrawCenteredText(sb, Subtitle.Value,
                new Vector2(panel.Center.X, subY + font.MeasureString("A").Y * subSc * 0.5f),
                new Color(120, 195, 215, (int)(190 * _alpha)), subSc);

            int divY = (int)(subY + font.MeasureString("A").Y * subSc + 12f);
            int divW = (int)(panel.Width * 0.55f);
            int divX = panel.Center.X - divW / 2;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(divX, divY, divW / 2 - 6, 1),
                new Color(70, 200, 220, (int)(150 * _alpha)));
            BaseManagerStyle.FillRect(sb,
                new Rectangle(divX + divW / 2 + 6, divY, divW / 2 - 6, 1),
                new Color(70, 200, 220, (int)(150 * _alpha)));
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.Center.X - 3, divY - 1, 6, 3),
                new Color(120, 230, 245, (int)(220 * _alpha)));

            float lineH = font.MeasureString("A").Y * bodySc + 6f;
            float statY = divY + 14f;
            float statX = panel.X + 36f;
            DrawStatLine(sb, font, statX, statY, Stat1.Value, bodySc);
            DrawStatLine(sb, font, statX, statY + lineH, Stat2.Value, bodySc);
            DrawStatLine(sb, font, statX, statY + lineH * 2f, Stat3.Value, bodySc);

            const int btnW = 130;
            const int btnH = 34;
            int btnY = panel.Bottom - 70;
            int gap = 28;
            int btnTotalW = btnW * 2 + gap;
            int btnX = panel.Center.X - btnTotalW / 2;

            _retryRect = new Rectangle(btnX, btnY, btnW, btnH);
            _exitRect = new Rectangle(btnX + btnW + gap, btnY, btnW, btnH);
            DrawPanelButton(sb, font, _retryRect, BtnRetry.Value, hot: true);
            DrawPanelButton(sb, font, _exitRect, BtnExit.Value, hot: false);

            BaseManagerStyle.DrawCenteredText(sb, Footer.Value,
                new Vector2(panel.Center.X, panel.Bottom - 18f),
                new Color(110, 180, 200, (int)(180 * _alpha)), footerSc);
        }

        private static void DrawStatLine(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font,
            float x, float y, string text, float scale) {
            Utils.DrawBorderString(sb, text, new Vector2(x, y),
                new Color(180, 230, 240, (int)(230 * _alpha)), scale);
        }

        private static void DrawPanelButton(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font,
            Rectangle rect, string text, bool hot) {
            bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
            //hot=RETRY主色，EXIT次色
            Color baseBg = hot ? new Color(20, 90, 110) : new Color(16, 60, 78);
            Color hoverBg = hot ? new Color(50, 175, 200) : new Color(40, 130, 150);
            Color baseBorder = hot ? new Color(70, 200, 230) : new Color(60, 150, 170);
            Color hoverBorder = hot ? new Color(120, 240, 255) : new Color(110, 220, 240);
            Color baseText = hot ? new Color(170, 235, 245) : new Color(160, 215, 225);
            Color hoverText = new Color(225, 250, 255);

            float pulse = hovered ? 1f : 0.85f + 0.15f * MathF.Sin(_shaderTimer * 5f);
            Color bg = (hovered ? hoverBg : baseBg) * (_alpha * 0.95f * pulse);
            Color border = (hovered ? hoverBorder : baseBorder) * _alpha;
            Color textCol = (hovered ? hoverText : baseText) * _alpha;

            BaseManagerStyle.FillRect(sb, rect, bg);
            BaseManagerStyle.StrokeRect(sb, rect, 1, border);
            BaseManagerStyle.DrawCenteredText(sb, text, rect.Center.ToVector2(), textCol, 0.78f);

            int capH = 6;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(rect.X - 2, rect.Y + rect.Height / 2 - capH, 4, capH * 2),
                border);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(rect.Right - 2, rect.Y + rect.Height / 2 - capH, 4, capH * 2),
                border);
        }
    }
}
