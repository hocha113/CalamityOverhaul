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
            Title = this.GetLocalization(nameof(Title), () => "训练完成");
            Subtitle = this.GetLocalization(nameof(Subtitle), () => "超梦节点");
            Stat1 = this.GetLocalization(nameof(Stat1), () => "[#] SHPC HUD 已就绪");
            Stat2 = this.GetLocalization(nameof(Stat2), () => "[#] 骇客时间 已就绪");
            Stat3 = this.GetLocalization(nameof(Stat3), () => "[#] 物块扫描 已就绪");
            BtnRetry = this.GetLocalization(nameof(BtnRetry), () => "重新训练");
            BtnExit = this.GetLocalization(nameof(BtnExit), () => "退出");
            Footer = this.GetLocalization(nameof(Footer), () => "请选择一项以继续");
        }

        private const int PanelW = 460;
        private const int PanelH = 280;

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

            CybCourseCardStyle.DrawPanelBg(sb, panel, _alpha, _shaderTimer, amber: false);
            DrawPanelContent(sb, panel);
        }

        private static void DrawPanelContent(SpriteBatch sb, Rectangle panel) {
            var font = FontAssets.MouseText.Value;
            float titleSc = 1.30f;
            float subSc = 0.62f;
            float bodySc = 0.74f;
            float footerSc = 0.55f;

            CybCourseCardStyle.DrawBreathLine(sb, panel, _alpha, _shaderTimer, new Color(80, 220, 245, 140));

            float titleY = panel.Y + 22f;
            BaseManagerStyle.DrawCenteredText(sb, Title.Value,
                new Vector2(panel.Center.X, titleY + font.MeasureString("A").Y * titleSc * 0.5f),
                new Color(80, 230, 250, (int)(255 * _alpha)), titleSc);

            float subY = titleY + font.MeasureString("A").Y * titleSc + 6f;
            BaseManagerStyle.DrawCenteredText(sb, Subtitle.Value,
                new Vector2(panel.Center.X, subY + font.MeasureString("A").Y * subSc * 0.5f),
                new Color(120, 195, 215, (int)(190 * _alpha)), subSc);

            int divY = (int)(subY + font.MeasureString("A").Y * subSc + 12f);
            CybCourseCardStyle.DrawDividerGem(sb, panel, divY, _alpha,
                new Color(70, 200, 220, 150), new Color(120, 230, 245, 220));

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
            var mouse = new Point(Main.mouseX, Main.mouseY);
            CybCourseCardStyle.DrawPanelButton(sb, _retryRect, BtnRetry.Value, hot: true, amber: false, _alpha, _shaderTimer, mouse);
            CybCourseCardStyle.DrawPanelButton(sb, _exitRect, BtnExit.Value, hot: false, amber: false, _alpha, _shaderTimer, mouse);

            BaseManagerStyle.DrawCenteredText(sb, Footer.Value,
                new Vector2(panel.Center.X, panel.Bottom - 18f),
                new Color(110, 180, 200, (int)(180 * _alpha)), footerSc);
        }

        private static void DrawStatLine(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font,
            float x, float y, string text, float scale) {
            Utils.DrawBorderString(sb, text, new Vector2(x, y),
                new Color(180, 230, 240, (int)(230 * _alpha)), scale);
        }
    }
}
