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

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel.CybCourses
{
    /// <summary>
    /// 教程末尾未绑定快捷键提醒，EntrustGuideCard 暖琥珀 variant
    /// </summary>
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

        //面板尺寸
        private const int PanelW = 540;
        private const int PanelMaxH = 560;
        private const int EdgePad = 10;
        private const int RowH = 32;
        //固定内容区高度（标题+副标题+分隔线+提示语+间距），按 MouseText 典型 fontH≈20 估算
        private const int HeaderOverhead = 152;
        //列表下方到面板底（按钮+脚注+内边距）
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

        //待提醒的关键快捷键集合，按教程涉及顺序排列
        //用 Func 包一层是为了在调用时再读取静态属性，避免类型初始化顺序问题
        private static readonly Func<ModKeybind>[] WatchedKeys = new Func<ModKeybind>[] {
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

        /// <summary>
        /// 收集未绑定快捷键；全已绑定则直接 Exit
        /// </summary>
        public static void ShowOrExit() {
            var unbound = CollectUnbound();
            if (unbound.Count == 0) {
                CybCourse.Exit();
                return;
            }
            _entries = unbound;
            _phase = Phase.FadeIn;
            _alpha = 0f;
            _prevMouseLeft = true;//阻止上一次鼠标点击的余波直接命中本面板按钮
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
                //教程被触发时按键系统必然已加载，这里只防御被外部误置 null
                ModKeybind kb = getter();
                if (kb == null) continue;

                //显式指定键盘输入模式，避免在手柄模式下错误读取手柄绑定列表
                var keys = kb.GetAssignedKeys(InputMode.Keyboard);
                if (keys != null && keys.Count > 0) continue;

                //ModKeybind 自带 DisplayName 本地化文本，直接复用，无需手拼键名
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
                        //淡出完成后再触发退出，避免视觉跳变
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

            //全屏蒙板，比完成面板再压暗一档以聚焦
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color(8, 4, 0, (int)(170 * _alpha)));

            //依据实际内容块高度精确计算面板高度，避免列表与底部按钮区互相压缩
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
                //variant=0 暖琥珀，提示性更强
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

            //cursor-Y 流式布局，从顶部向下依次排列各区域，杜绝遮挡
            float curY = panel.Y + 22f;

            //顶部呼吸光带
            float breath = 0.55f + 0.45f * MathF.Sin(_shaderTimer * 4f);
            BaseManagerStyle.FillRect(sb,
                new Rectangle(panel.X + 14, panel.Y + 8, panel.Width - 28, 2),
                new Color(255, 200, 90, (int)(150 * _alpha * breath)));

            //标题
            float titleH = fontH * titleSc;
            BaseManagerStyle.DrawCenteredText(sb, Title.Value,
                new Vector2(panel.Center.X, curY + titleH * 0.5f),
                new Color(255, 215, 130, (int)(255 * _alpha)), titleSc);
            curY += titleH + 8f;

            //副标题
            float subH = fontH * subSc;
            BaseManagerStyle.DrawCenteredText(sb, Subtitle.Value,
                new Vector2(panel.Center.X, curY + subH * 0.5f),
                new Color(245, 195, 140, (int)(200 * _alpha)), subSc);
            curY += subH + 14f;

            //装饰分隔线
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

            //提示语
            string[] hintLines = Hint.Value.Split('\n');
            float hintLineH = fontH * bodySc + 4f;
            for (int i = 0; i < hintLines.Length; i++) {
                BaseManagerStyle.DrawCenteredText(sb, hintLines[i],
                    new Vector2(panel.Center.X, curY + hintLineH * 0.5f),
                    new Color(245, 220, 175, (int)(225 * _alpha)), bodySc);
                curY += hintLineH;
            }
            curY += 14f;

            //列表区
            int listX = panel.X + 40;
            int listRight = panel.Right - 40;
            for (int i = 0; i < _entries.Count; i++) {
                DrawKeyRow(sb, font, listX, listRight, (int)curY, _entries[i], i);
                curY += RowH;
            }

            //列表与按钮之间的间距
            curY += 14f;

            //底部按钮区：跟随 curY
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

            //底部脚注
            BaseManagerStyle.DrawCenteredText(sb, Footer.Value,
                new Vector2(panel.Center.X, curY + fontH * footerSc * 0.5f),
                new Color(225, 185, 130, (int)(180 * _alpha)), footerSc);
        }

        private static void DrawKeyRow(SpriteBatch sb, ReLogic.Graphics.DynamicSpriteFont font,
            int x, int right, int y, KeyEntry entry, int index) {
            float a = _alpha;
            //行底色，与暖琥珀主题协调
            var rowRect = new Rectangle(x, y + 2, right - x, RowH - 4);
            Color rowBg = (index & 1) == 0
                ? new Color(40, 22, 8, (int)(110 * a))
                : new Color(50, 28, 10, (int)(140 * a));
            BaseManagerStyle.FillRect(sb, rowRect, rowBg);
            //左侧高亮条
            BaseManagerStyle.FillRect(sb,
                new Rectangle(x - 4, y + 4, 3, RowH - 8),
                new Color(255, 180, 80, (int)(220 * a)));

            //项目编号
            string idx = $"{(index + 1):D2}.";
            Utils.DrawBorderString(sb, idx,
                new Vector2(x + 6, y + 7),
                new Color(255, 200, 110, (int)(220 * a)), 0.62f);

            //功能名
            Utils.DrawBorderString(sb, entry.DisplayName,
                new Vector2(x + 38, y + 6),
                new Color(255, 230, 190, (int)(240 * a)), 0.72f);

            //右侧未绑定徽标，统一使用本地化的 UnboundLabel
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
            //hot=主选项（我已知晓）
            Color baseBg = hot ? new Color(110, 70, 18) : new Color(72, 50, 22);
            Color hoverBg = hot ? new Color(200, 140, 40) : new Color(150, 110, 50);
            Color baseBorder = hot ? new Color(240, 190, 90) : new Color(190, 150, 80);
            Color hoverBorder = hot ? new Color(255, 230, 150) : new Color(240, 200, 130);
            Color baseText = hot ? new Color(255, 230, 180) : new Color(230, 205, 160);
            Color hoverText = new Color(255, 250, 220);

            float pulse = hovered ? 1f : (0.85f + 0.15f * MathF.Sin(_shaderTimer * 5f));
            Color bg = (hovered ? hoverBg : baseBg) * (_alpha * 0.95f * pulse);
            Color border = (hovered ? hoverBorder : baseBorder) * _alpha;
            Color textCol = (hovered ? hoverText : baseText) * _alpha;

            BaseManagerStyle.FillRect(sb, rect, bg);
            BaseManagerStyle.StrokeRect(sb, rect, 1, border);
            BaseManagerStyle.DrawCenteredText(sb, text, rect.Center.ToVector2(), textCol, 0.78f);

            //左右端帽
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
