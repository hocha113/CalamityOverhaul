using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    /// <summary>教程末未绑键提醒,EntrustGuideCard暖琥珀</summary>
    internal class CybCourseKeyBindReminderPanel : ModSystem, ILocalizedModType
    {
        private enum Phase { Hidden, FadeIn, Idle, FadeOut }

        public string LocalizationCategory => "ADV.Shepel";

        public static LocalizedText Title { get; private set; }
        public static LocalizedText Subtitle { get; private set; }
        public static LocalizedText Hint { get; private set; }
        public static LocalizedText UnboundLabel { get; private set; }
        public static LocalizedText BtnConfirm { get; private set; }
        public static LocalizedText BtnLater { get; private set; }
        public static LocalizedText Footer { get; private set; }

        public override void SetStaticDefaults() {
            Title = this.GetLocalization(nameof(Title), () => "绑定警报");
            Subtitle = this.GetLocalization(nameof(Subtitle), () => "核心快捷键未绑定");
            Hint = this.GetLocalization(nameof(Hint), ()
                => @"以下快捷键当前未绑定。离开超梦后，您将无法正常使用其对应的功能。
请前往 [设置 → 控制] 中为它们分配按键。");
            UnboundLabel = this.GetLocalization(nameof(UnboundLabel), () => "未绑定");
            BtnConfirm = this.GetLocalization(nameof(BtnConfirm), () => "已知悉");
            BtnLater = this.GetLocalization(nameof(BtnLater), () => "稍后处理");
            Footer = this.GetLocalization(nameof(Footer), ()
                => "选择任意选项都将断开超梦连接。您可以随时在主世界的设置中调整按键绑定。");
        }

        private const int PanelW = 540;
        private const int PanelMaxH = 560;
        private const int EdgePad = 10;
        private const int RowH = 32;
        //标题区等固定高度,fontH≈20估
        private const int HeaderOverhead = 152;
        //列表下按钮脚注区
        private const int FooterOverhead = 100;

        public static bool Visible => _phase != Phase.Hidden;

        private static Phase _phase = Phase.Hidden;
        private static float _alpha = 0f;
        private static float _shaderTimer = 0f;
        private static bool _prevMouseLeft = false;
        private static Rectangle _confirmRect = Rectangle.Empty;
        private static Rectangle _laterRect = Rectangle.Empty;
        private static Rectangle _panelRect = Rectangle.Empty;
        private static List<KeyEntry> _entries = new();

        //Func延迟读键绑,避初始化顺序
        private static readonly Func<ModKeybind>[] WatchedKeys = new Func<ModKeybind>[] {
            //转盘键是三套快捷盘的唯一入口，未绑定比表里任何一个都严重
            () => CWRKeySystem.RadialWheel_Key,
            () => CWRKeySystem.HackTime_Toggle,
            () => CWRKeySystem.CyberBanish_Key,
            () => CWRKeySystem.CyberFreeze_Key,
            () => CWRKeySystem.CyberwareSkill_Key,
            () => CWRKeySystem.VoidTimeShift_Key,
            () => CWRKeySystem.WeponSkill_Q,
            () => CWRKeySystem.WeponSkill_R,
        };

        private readonly struct KeyEntry
        {
            public readonly string DisplayName;
            public KeyEntry(string displayName) { DisplayName = displayName; }
        }

        public override void OnWorldUnload() => Hide();

        /// <summary>未绑则Show,全绑则Exit</summary>
        public static void ShowOrExit() {
            var unbound = CollectUnbound();
            if (unbound.Count == 0) {
                CybCourse.Exit();
                return;
            }
            _entries = unbound;
            _phase = Phase.FadeIn;
            _alpha = 0f;
            _prevMouseLeft = true;//click余波,置true避误触
        }

        public static void Hide() {
            _phase = Phase.Hidden;
            _alpha = 0f;
            _confirmRect = Rectangle.Empty;
            _laterRect = Rectangle.Empty;
            _panelRect = Rectangle.Empty;
            _entries = new List<KeyEntry>();
        }

        private static List<KeyEntry> CollectUnbound() {
            var list = new List<KeyEntry>();
            foreach (var getter in WatchedKeys) {
                ModKeybind kb = getter();
                if (kb == null) continue;

                //Keyboard模式,避读手柄绑
                var keys = kb.GetAssignedKeys(InputMode.Keyboard);
                if (keys != null && keys.Count > 0) continue;

                string display = kb.DisplayName?.Value;
                if (string.IsNullOrWhiteSpace(display)) continue;
                list.Add(new KeyEntry(display));
            }
            return list;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (Main.dedServ || Main.gameMenu) return;
            if (!CybCourseWorld.Active) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _shaderTimer += dt * 0.7f;
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
                    HandleClicks();
                    break;
                case Phase.FadeOut:
                    _alpha = MathHelper.Lerp(_alpha, 0f, 0.18f);
                    if (_alpha < 0.02f) {
                        //淡出完再Exit
                        Hide();
                        CybCourse.Exit();
                    }
                    break;
            }

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
            if (_confirmRect.Contains(mx, my) || _laterRect.Contains(mx, my)) {
                Main.mouseLeft = false;
                _phase = Phase.FadeOut;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (_phase == Phase.Hidden) return;
            if (_alpha < 0.01f) return;

            int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
            if (idx == -1) return;

            layers.Insert(idx, new LegacyGameInterfaceLayer("CWRMod: CybCourse KeyBind Reminder Panel",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                }, InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            //蒙板比完成面板更暗
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(8, 4, 0, (int)(170 * _alpha)));

            //按内容算dynH,避挤压
            int dynH = Math.Clamp(HeaderOverhead + _entries.Count * RowH + FooterOverhead, HeaderOverhead + FooterOverhead, PanelMaxH);
            int cx = (Main.screenWidth - PanelW) / 2;
            int cy = (Main.screenHeight - dynH) / 2;
            float slideY = (1f - _alpha) * 28f;
            int finalY = (int)MathHelper.Clamp(cy + (int)slideY, 8, Math.Max(8, Main.screenHeight - dynH - 8));
            int finalX = (int)MathHelper.Clamp(cx, 8, Math.Max(8, Main.screenWidth - PanelW - 8));
            var panel = new Rectangle(finalX, finalY, PanelW, dynH);
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
                //uVariant=0暖琥珀
                effect.Parameters["uVariant"]?.SetValue(0f);
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
                sb.Draw(VaultAsset.placeholder2.Value, panel, new Color(20, 14, 4, (int)(220 * _alpha)));
                BaseManagerStyle.StrokeRect(sb, panel, 1, new Color(220, 170, 70, (int)(170 * _alpha)));
            }
        }

        private static void DrawPanelContent(SpriteBatch sb, Rectangle panel) {
            var font = FontAssets.MouseText.Value;
            float fontH = font.MeasureString("A").Y;
            const float titleSc = 1.20f;
            const float subSc = 0.64f;
            const float bodySc = 0.68f;
            const float footerSc = 0.55f;

            float curY = panel.Y + 22f;

            float breath = 0.55f + 0.45f * MathF.Sin(_shaderTimer * 4f);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.X + 14, panel.Y + 8, panel.Width - 28, 2),
                new Color(255, 200, 90, (int)(150 * _alpha * breath)));

            float titleH = fontH * titleSc;
            BaseManagerStyle.DrawCenteredText(sb, Title.Value,
                new Vector2(panel.Center.X, curY + titleH * 0.5f),
                new Color(255, 215, 130, (int)(255 * _alpha)), titleSc);
            curY += titleH + 8f;

            float subH = fontH * subSc;
            BaseManagerStyle.DrawCenteredText(sb, Subtitle.Value,
                new Vector2(panel.Center.X, curY + subH * 0.5f),
                new Color(245, 195, 140, (int)(200 * _alpha)), subSc);
            curY += subH + 14f;

            int divW = (int)(panel.Width * 0.55f);
            int divX = panel.Center.X - divW / 2;
            BaseManagerStyle.FillRect(sb,
                new Rectangle(divX, (int)curY, divW / 2 - 6, 1),
                new Color(220, 170, 80, (int)(160 * _alpha)));
            BaseManagerStyle.FillRect(sb,
                new Rectangle(divX + divW / 2 + 6, (int)curY, divW / 2 - 6, 1),
                new Color(220, 170, 80, (int)(160 * _alpha)));
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.Center.X - 3, (int)curY - 1, 6, 3),
                new Color(255, 220, 140, (int)(220 * _alpha)));
            curY += 14f;

            string[] hintLines = Hint.Value.Split('\n');
            float hintLineH = fontH * bodySc + 4f;
            for (int i = 0; i < hintLines.Length; i++) {
                BaseManagerStyle.DrawCenteredText(sb, hintLines[i],
                    new Vector2(panel.Center.X, curY + hintLineH * 0.5f),
                    new Color(245, 220, 175, (int)(225 * _alpha)), bodySc);
                curY += hintLineH;
            }
            curY += 14f;

            int listX = panel.X + 40;
            int listRight = panel.Right - 40;
            for (int i = 0; i < _entries.Count; i++) {
                DrawKeyRow(sb, font, listX, listRight, (int)curY, _entries[i], i);
                curY += RowH;
            }

            curY += 14f;

            const int btnW = 150;
            const int btnH = 34;
            int gap = 28;
            int btnTotalW = btnW * 2 + gap;
            int btnX = panel.Center.X - btnTotalW / 2;

            _confirmRect = new Rectangle(btnX, (int)curY, btnW, btnH);
            _laterRect = new Rectangle(btnX + btnW + gap, (int)curY, btnW, btnH);
            DrawPanelButton(sb, _confirmRect, BtnConfirm.Value, hot: true);
            DrawPanelButton(sb, _laterRect, BtnLater.Value, hot: false);
            curY += btnH + 14f;

            BaseManagerStyle.DrawCenteredText(sb, Footer.Value,
                new Vector2(panel.Center.X, curY + fontH * footerSc * 0.5f),
                new Color(225, 185, 130, (int)(180 * _alpha)), footerSc);
        }

        private static void DrawKeyRow(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font,
            int x, int right, int y, KeyEntry entry, int index) {
            float a = _alpha;
            var rowRect = new Rectangle(x, y + 2, right - x, RowH - 4);
            Color rowBg = (index & 1) == 0
                ? new Color(40, 22, 8, (int)(110 * a))
                : new Color(50, 28, 10, (int)(140 * a));
            BaseManagerStyle.FillRect(sb, rowRect, rowBg);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(x - 4, y + 4, 3, RowH - 8),
                new Color(255, 180, 80, (int)(220 * a)));

            string idx = $"{index + 1:D2}.";
            Utils.DrawBorderString(sb, idx,
                new Vector2(x + 6, y + 7),
                new Color(255, 200, 110, (int)(220 * a)), 0.62f);

            Utils.DrawBorderString(sb, entry.DisplayName,
                new Vector2(x + 38, y + 6),
                new Color(255, 230, 190, (int)(240 * a)), 0.72f);

            string label = UnboundLabel.Value;
            float labelSc = 0.62f;
            Vector2 labelSize = font.MeasureString(label) * labelSc;
            Rectangle tagRect = new(
                right - (int)labelSize.X - 18,
                y + 4,
                (int)labelSize.X + 14,
                RowH - 8);
            BaseManagerStyle.FillRect(sb, tagRect,
                new Color(120, 36, 28, (int)(180 * a)));
            BaseManagerStyle.StrokeRect(sb, tagRect, 1,
                new Color(255, 120, 90, (int)(220 * a)));
            Utils.DrawBorderString(sb, label,
                new Vector2(tagRect.X + 7, tagRect.Y + (tagRect.Height - labelSize.Y) * 0.5f - 1),
                new Color(255, 215, 200, (int)(240 * a)), labelSc);
        }

        private static void DrawPanelButton(SpriteBatch sb, Rectangle rect, string text, bool hot) {
            bool hovered = rect.Contains(Main.mouseX, Main.mouseY);
            //hot=主选项
            Color baseBg = hot ? new Color(110, 70, 18) : new Color(72, 50, 22);
            Color hoverBg = hot ? new Color(200, 140, 40) : new Color(150, 110, 50);
            Color baseBorder = hot ? new Color(240, 190, 90) : new Color(190, 150, 80);
            Color hoverBorder = hot ? new Color(255, 230, 150) : new Color(240, 200, 130);
            Color baseText = hot ? new Color(255, 230, 180) : new Color(230, 205, 160);
            Color hoverText = new Color(255, 250, 220);

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
