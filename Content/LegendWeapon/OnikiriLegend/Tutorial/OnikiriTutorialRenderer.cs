using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程 UI 渲染层（结构对齐 <see cref="HalibutLegend.UI.HalibutHudLead"/>，
    /// 面板走 <see cref="OniShaderPanel"/> / <see cref="OniBrush"/> 和纸朱印语汇，而非 DrawSeaPanel）。
    /// </summary>
    internal static class OnikiriTutorialRenderer
    {
        private static float _cardAnim;
        private static float _shaderTime;
        private static int _lastStep = -1;
        private const float AnimSpeed = 0.12f;
        private const int EdgePad = 10;
        private const int StuckFramesBeforeSkip = 60 * 9;
        private const int CardW = 336;

        private readonly struct GLine
        {
            public readonly string Text;
            public readonly float Scale;
            public readonly Color Color;
            public GLine(string text, float scale, Color color) {
                Text = text; Scale = scale; Color = color;
            }
        }

        internal static void Draw() {
            if (!OnikiriTutorialFlow.IsRunning) {
                _cardAnim = 0f;
                _lastStep = -1;
                return;
            }

            int step = OnikiriTutorialFlow.CurrentStep;
            if (step != _lastStep) {
                _cardAnim = 0f;
                _lastStep = step;
            }
            _cardAnim = MathHelper.Lerp(_cardAnim, 1f, AnimSpeed);
            _shaderTime += 0.016f;
            if (_cardAnim < 0.02f) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            float time = Main.GlobalTimeWrappedHourly;
            float a = _cardAnim;

            HudFocusSnapshot focus = ResolveFocus(step);
            if (focus != null) {
                DrawHighlightRect(sb, focus.Rect, time, a);
            }

            DrawStepCard(sb, step, focus, time, a);
        }

        private static HudFocusSnapshot ResolveFocus(int step) {
            string tag = step switch {
                OnikiriTutorialFlow.Step_HudIntro => OnikiriTutorialTargets.Tag_VigorStroke,
                OnikiriTutorialFlow.Step_Register => OnikiriTutorialTargets.Tag_StanceSheath,
                OnikiriTutorialFlow.Step_Mei => OniMeiUI.Instance?.IsOpen == true
                    ? OnikiriTutorialTargets.Tag_MeiSlotNakago
                    : OnikiriTutorialTargets.Tag_TalismanStrip,
                OnikiriTutorialFlow.Step_DashJudge => OnikiriTutorialTargets.Tag_VigorStroke,
                OnikiriTutorialFlow.Step_Annihilate or OnikiriTutorialFlow.Step_Finale
                    => OnikiriTutorialTargets.Tag_StanceSheath,
                OnikiriTutorialFlow.Step_Sakura or OnikiriTutorialFlow.Step_Dismember
                    or OnikiriTutorialFlow.Step_Close => OnikiriTutorialTargets.Tag_DomainEye,
                _ => null,
            };
            return tag == null ? null : OnikiriTutorialTargets.Get(tag);
        }

        #region 高亮
        private static void DrawHighlightRect(SpriteBatch sb, Rectangle rect, float time, float alpha) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return;
            }

            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 2.4f));
            Rectangle r = rect;
            r.Inflate(5, 5);

            //外晕（加法，不压暗）
            OniBrush.DrawBacklight(sb, r.Center.ToVector2(),
                MathF.Max(r.Width, r.Height) * 0.55f,
                OnikiriUITheme.Bright, alpha * 0.22f * pulse);

            Color edge = OnikiriUITheme.Bright * ((0.55f + pulse * 0.35f) * alpha);
            DrawDashedBorder(sb, px, r, edge, 6f, 4f, time * -22f);

            r.Inflate(3, 3);
            DrawDashedBorder(sb, px, r, OnikiriUITheme.Deep * (0.35f * alpha), 6f, 4f, time * -22f);
        }

        private static void DrawDashedBorder(SpriteBatch sb, Texture2D px, Rectangle rect,
            Color color, float dash, float gap, float flow) {
            DrawDashedSeg(sb, px, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), color, dash, gap, flow);
            DrawDashedSeg(sb, px, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), color, dash, gap, flow);
            DrawDashedSeg(sb, px, new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Left, rect.Bottom), color, dash, gap, flow);
            DrawDashedSeg(sb, px, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Left, rect.Top), color, dash, gap, flow);
        }

        private static void DrawDashedSeg(SpriteBatch sb, Texture2D px, Vector2 from, Vector2 to,
            Color color, float dash, float gap, float flow) {
            Vector2 edge = to - from;
            float len = edge.Length();
            if (len < 1f) {
                return;
            }
            Vector2 dir = edge / len;
            float rot = dir.ToRotation();
            float period = dash + gap;
            float t = ((flow % period) + period) % period;
            for (float d = -t; d < len; d += period) {
                float a0 = Math.Max(0f, d);
                float a1 = Math.Min(len, d + dash);
                if (a1 <= a0) {
                    continue;
                }
                Vector2 p = from + dir * a0;
                sb.Draw(px, p, new Rectangle(0, 0, 1, 1), color, rot, new Vector2(0f, 0.5f),
                    new Vector2(a1 - a0, 1.6f), SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 步骤卡片
        private static void DrawStepCard(SpriteBatch sb, int step, HudFocusSnapshot focus, float time, float a) {
            if (!TryGetStepCopy(step, out LocalizedText title, out LocalizedText body, out LocalizedText prompt)) {
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float contentW = CardW - 32f;
            string promptText = FormatPrompt(step, prompt);
            GLine[] lines = string.IsNullOrEmpty(promptText)
                ? [new(body.Value, 0.74f, OnikiriUITheme.TextDim)]
                : [
                    new(body.Value, 0.74f, OnikiriUITheme.TextDim),
                    new(promptText, 0.78f, OnikiriUITheme.HotWhite),
                ];
            int cardH = MeasureCardH(font, 0.9f, lines, contentW);
            Rectangle card = PlaceCard(focus, cardH, a);

            if (focus != null) {
                DrawConnector(sb, card, focus.Rect.Center.ToVector2(), a, time);
            }

            DrawCardPanel(sb, card, a, time);
            DrawCardContent(sb, font, card, title.Value, 0.9f, lines, a);

            //交互钮：认知步给「已知晓」；开簿/改铭给助手钮；卡住后给出跳过
            if (step == OnikiriTutorialFlow.Step_HudIntro) {
                if (DrawActionButton(sb, font, card, OnikiriTutorialLead.NextBtn.Value, OnikiriUITheme.Bright, time, a)) {
                    OnikiriTutorialFlow.RequestAdvance();
                }
            }
            else if (step == OnikiriTutorialFlow.Step_Register) {
                if (DrawActionButton(sb, font, card, OnikiriTutorialLead.OpenRegisterBtn.Value, OnikiriUITheme.Bright, time, a)) {
                    OniRegisterUI.Instance?.Open();
                }
                else if (OnikiriTutorialFlow.StepTimer > StuckFramesBeforeSkip
                    && DrawSecondaryButton(sb, font, card, OnikiriTutorialLead.SkipBtn.Value, time, a)) {
                    OnikiriTutorialFlow.RequestAdvance();
                }
            }
            else if (step == OnikiriTutorialFlow.Step_Mei) {
                bool meiOpen = OniMeiUI.Instance?.IsOpen ?? false;
                if (meiOpen) {
                    if (DrawActionButton(sb, font, card, OnikiriTutorialLead.NextBtn.Value, OnikiriUITheme.GoldInlay, time, a)) {
                        OnikiriTutorialFlow.RequestAdvance();
                    }
                }
                else if (DrawActionButton(sb, font, card, OnikiriTutorialLead.OpenMeiBtn.Value, OnikiriUITheme.GoldInlay, time, a)) {
                    OniMeiUI.Instance?.Open();
                }
                else if (OnikiriTutorialFlow.StepTimer > StuckFramesBeforeSkip
                    && DrawSecondaryButton(sb, font, card, OnikiriTutorialLead.SkipBtn.Value, time, a)) {
                    OnikiriTutorialFlow.RequestAdvance();
                }
            }
            else if (OnikiriTutorialFlow.StepTimer > StuckFramesBeforeSkip
                && DrawActionButton(sb, font, card, OnikiriTutorialLead.SkipBtn.Value, OnikiriUITheme.TextDim, time, a)) {
                OnikiriTutorialFlow.RequestAdvance();
            }

            //卡片区域吞点击，避免穿透打世界
            if (card.Contains(OnikiriUITheme.UIMouse.ToPoint())) {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private static bool TryGetStepCopy(int step, out LocalizedText title, out LocalizedText body, out LocalizedText prompt) {
            title = body = prompt = null;
            switch (step) {
                case OnikiriTutorialFlow.Step_HudIntro:
                    title = OnikiriTutorialLead.HudTitle; body = OnikiriTutorialLead.HudBody; prompt = OnikiriTutorialLead.HudPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    title = OnikiriTutorialLead.RegisterTitle; body = OnikiriTutorialLead.RegisterBody; prompt = OnikiriTutorialLead.RegisterPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    title = OnikiriTutorialLead.MeiTitle; body = OnikiriTutorialLead.MeiBody; prompt = OnikiriTutorialLead.MeiPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Combo:
                    title = OnikiriTutorialLead.ComboTitle; body = OnikiriTutorialLead.ComboBody; prompt = OnikiriTutorialLead.ComboPrompt;
                    break;
                case OnikiriTutorialFlow.Step_DashJudge:
                    title = OnikiriTutorialLead.DashTitle; body = OnikiriTutorialLead.DashBody; prompt = OnikiriTutorialLead.DashPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Zanshin:
                    title = OnikiriTutorialLead.ZanshinTitle; body = OnikiriTutorialLead.ZanshinBody; prompt = OnikiriTutorialLead.ZanshinPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Sakura:
                    title = OnikiriTutorialLead.SakuraTitle; body = OnikiriTutorialLead.SakuraBody; prompt = OnikiriTutorialLead.SakuraPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Annihilate:
                    title = OnikiriTutorialLead.AnnihilateTitle; body = OnikiriTutorialLead.AnnihilateBody; prompt = OnikiriTutorialLead.AnnihilatePrompt;
                    break;
                case OnikiriTutorialFlow.Step_Finale:
                    title = OnikiriTutorialLead.FinaleTitle; body = OnikiriTutorialLead.FinaleBody; prompt = OnikiriTutorialLead.FinalePrompt;
                    break;
                case OnikiriTutorialFlow.Step_Dismember:
                    title = OnikiriTutorialLead.DismemberTitle; body = OnikiriTutorialLead.DismemberBody; prompt = OnikiriTutorialLead.DismemberPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Close:
                    title = OnikiriTutorialLead.CloseTitle; body = OnikiriTutorialLead.CloseBody; prompt = OnikiriTutorialLead.ClosePrompt;
                    break;
                default:
                    return false;
            }
            return title != null && body != null;
        }

        private static string FormatPrompt(int step, LocalizedText prompt) {
            if (prompt == null) {
                return null;
            }
            string raw = prompt.Value;
            if (string.IsNullOrEmpty(raw) || !raw.Contains("{0}")) {
                return raw;
            }
            string key = step switch {
                OnikiriTutorialFlow.Step_HudIntro or OnikiriTutorialFlow.Step_Register or OnikiriTutorialFlow.Step_Mei
                    => CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value),
                OnikiriTutorialFlow.Step_DashJudge
                    => CWRKeySystem.Onikiri_FlashStep.ToTooltipString(CWRKeySystem.Notbound.Value),
                OnikiriTutorialFlow.Step_Sakura or OnikiriTutorialFlow.Step_Dismember or OnikiriTutorialFlow.Step_Close
                    => CWRKeySystem.Onikiri_DomainFlip.ToTooltipString(CWRKeySystem.Notbound.Value),
                _ => CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value),
            };
            return string.Format(raw, key);
        }

        private static Rectangle PlaceCard(HudFocusSnapshot focus, int cardH, float a) {
            float ease = VaultUtils.EaseOutCubic(a);
            float slide = (1f - ease) * 28f;
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;

            float x, y;
            if (focus != null) {
                Rectangle f = focus.Rect;
                //优先放在焦点右侧；放不下则左侧；再钳到屏内
                x = f.Right + 18f - slide;
                if (x + CardW > sw - 16f) {
                    x = f.Left - CardW - 18f + slide;
                }
                y = f.Center.Y - cardH * 0.5f;
            }
            else {
                x = sw - CardW - 24f;
                y = sh * 0.38f;
            }

            x = MathHelper.Clamp(x, 16f, sw - CardW - 16f);
            y = MathHelper.Clamp(y, 16f, sh - cardH - 16f);
            return new Rectangle((int)x, (int)y, CardW, cardH);
        }

        private static void DrawCardPanel(SpriteBatch sb, Rectangle card, float a, float time) {
            Texture2D px = VaultAsset.placeholder2.Value;
            OniBrush.DrawPanelDropShadow(sb, card.Center.ToVector2(),
                new Vector2(card.Width, card.Height), a);

            if (OniShaderPanel.Available) {
                OniShaderPanel.Draw(sb, card, Math.Min(1f, a * 1.35f), MathHelper.Lerp(0.82f, 1f, a),
                    _shaderTime, EdgePad, Color.White);
            }
            else {
                sb.Draw(px, card, new Rectangle(0, 0, 1, 1), OnikiriUITheme.Ink * (a * 0.96f));
                sb.Draw(px, new Rectangle(card.X, card.Y, card.Width, 1), new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.Deep * (a * 0.7f));
                sb.Draw(px, new Rectangle(card.X, card.Bottom - 1, card.Width, 1), new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.Deep * (a * 0.45f));
                sb.Draw(px, new Rectangle(card.X, card.Y, 1, card.Height), new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.Bright * (a * 0.28f));
                sb.Draw(px, new Rectangle(card.Right - 1, card.Y, 1, card.Height), new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.Dark * (a * 0.5f));
            }

            //顶缘朱丝 + 纸垂
            OniBrush.DrawTaperedSlash(sb,
                new Vector2(card.X + 10f, card.Y + 1f),
                new Vector2(card.Right - 10f, card.Y + 2f), 1.5f, 0.6f, a * 0.55f);
            OniBrush.DrawShide(sb, card, a * 0.85f, time);
            OniBrush.DrawSealGlyph(sb, new Vector2(card.X + 22f, card.Y + 18f), 12f, a * 0.95f, time * 0.02f);
        }

        private static void DrawCardContent(SpriteBatch sb, DynamicSpriteFont font, Rectangle card,
            string title, float titleScale, GLine[] body, float a) {
            float px = card.X + 36f;
            float py = card.Y + 10f;
            float wrap = card.Width - 48f;

            Utils.DrawBorderString(sb, title, new Vector2(px + 1f, py + 1f), Color.Black * (0.45f * a), titleScale);
            Utils.DrawBorderString(sb, title, new Vector2(px, py), OnikiriUITheme.HotWhite * a, titleScale);
            py += font.MeasureString("A").Y * titleScale + 6f;

            OniBrush.DrawTaperedSlash(sb,
                new Vector2(card.X + 14f, py),
                new Vector2(card.Right - 14f, py - 1f), 1.8f, 1.0f, a * 0.85f);
            py += 10f;

            foreach (GLine gl in body) {
                py = DrawBody(sb, font, gl.Text, px - 18f, py, wrap, gl.Scale, gl.Color, a) + 4f;
            }
        }

        private static float DrawBody(SpriteBatch sb, DynamicSpriteFont font, string text,
            float x, float y, float wrapPx, float scale, Color color, float a) {
            if (string.IsNullOrEmpty(text)) {
                return y;
            }
            string[] wrapped = VaultUtils.WrapTextArray(text, font, Math.Max(8, (int)(wrapPx / scale)), 99, out _);
            float lineH = font.MeasureString("A").Y * scale + 3f;
            foreach (string wl in wrapped) {
                if (string.IsNullOrEmpty(wl)) {
                    continue;
                }
                string line = wl.TrimEnd('-', ' ');
                Utils.DrawBorderString(sb, line, new Vector2(x + 1f, y + 1f), Color.Black * (0.5f * a), scale);
                Utils.DrawBorderString(sb, line, new Vector2(x, y), color * a, scale);
                y += lineH;
            }
            return y;
        }

        private static float MeasureWrapH(DynamicSpriteFont font, string text, float scale, float wrapPx) {
            if (string.IsNullOrEmpty(text)) {
                return 0f;
            }
            int n = 0;
            foreach (string s in VaultUtils.WrapTextArray(text, font, Math.Max(8, (int)(wrapPx / scale)), 99, out _)) {
                if (!string.IsNullOrEmpty(s)) {
                    n++;
                }
            }
            return Math.Max(n, 1) * (font.MeasureString("A").Y * scale + 3f);
        }

        private static int MeasureCardH(DynamicSpriteFont font, float titleScale, GLine[] body, float contentW) {
            float la = font.MeasureString("A").Y;
            float h = 14f + (la * titleScale + 8f) + 10f;
            foreach (GLine gl in body) {
                h += MeasureWrapH(font, gl.Text, gl.Scale, contentW) + 4f;
            }
            return (int)MathF.Ceiling(h + 42f);
        }

        private static void DrawConnector(SpriteBatch sb, Rectangle card, Vector2 target, float a, float time) {
            Vector2 from = card.Center.X < target.X
                ? new Vector2(card.Right - 4f, card.Center.Y)
                : new Vector2(card.X + 4f, card.Center.Y);
            Color c0 = OnikiriUITheme.Bright * (0.55f * a);
            Color c1 = OnikiriUITheme.Deep * (0.08f * a);
            OniBrush.DrawGradientLine(sb, from, target, c0, c1, 1.3f);
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 3.2f);
            Texture2D px = VaultAsset.placeholder2.Value;
            sb.Draw(px, target, new Rectangle(0, 0, 1, 1), OnikiriUITheme.HotWhite * (0.7f * a * pulse),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5.5f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 按钮
        private static bool DrawActionButton(SpriteBatch sb, DynamicSpriteFont font, Rectangle card,
            string text, Color accent, float time, float a) {
            const int btnH = 26;
            Vector2 size = font.MeasureString(text) * 0.76f;
            int btnW = Math.Max(98, (int)size.X + 28);
            var rect = new Rectangle(card.Right - btnW - 14, card.Bottom - btnH - 12, btnW, btnH);
            return DrawPaperButton(sb, font, rect, text, accent, time, a);
        }

        private static bool DrawSecondaryButton(SpriteBatch sb, DynamicSpriteFont font, Rectangle card,
            string text, float time, float a) {
            const int btnH = 22;
            Vector2 size = font.MeasureString(text) * 0.68f;
            int btnW = Math.Max(72, (int)size.X + 20);
            var rect = new Rectangle(card.X + 14, card.Bottom - btnH - 14, btnW, btnH);
            return DrawPaperButton(sb, font, rect, text, OnikiriUITheme.TextDim, time, a * 0.9f, 0.68f);
        }

        private static bool DrawPaperButton(SpriteBatch sb, DynamicSpriteFont font, Rectangle rect,
            string text, Color accent, float time, float a, float textScale = 0.76f) {
            Texture2D px = VaultAsset.placeholder2.Value;
            bool hovered = rect.Contains(OnikiriUITheme.UIMouse.ToPoint());
            float hi = hovered ? 1f : 0f;

            //裱墨小牌：实心底 + 朱红压边（不用同心扩层假羽化）
            Color fill = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, 0.35f + hi * 0.4f);
            sb.Draw(px, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height),
                new Rectangle(0, 0, 1, 1), new Color(8, 2, 5) * (a * 0.35f));
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), fill * (a * 0.94f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1),
                accent * ((0.45f + hi * 0.4f) * a));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1),
                OnikiriUITheme.Deep * (0.55f * a));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1),
                accent * ((0.35f + hi * 0.35f) * a));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1),
                OnikiriUITheme.Dark * (0.55f * a));

            if (hi > 0.05f) {
                float sweep = (time * 0.9f) % 1.2f / 1.2f;
                float sx = MathHelper.Lerp(rect.X + 4f, rect.Right - 4f, sweep);
                OniBrush.DrawSoftStreak(sb, new Vector2(sx, rect.Center.Y), -0.9f, rect.Height * 0.85f,
                    1.4f, OnikiriUITheme.HotWhite, a * 0.35f * hi, 0.6f);
            }

            Vector2 tSize = font.MeasureString(text) * textScale;
            Vector2 tPos = rect.Center.ToVector2() - tSize * 0.5f + new Vector2(0f, -1f);
            Color tCol = Color.Lerp(OnikiriUITheme.Paper, accent, 0.25f + hi * 0.45f);
            Utils.DrawBorderString(sb, text, tPos, tCol * a, textScale);

            if (hovered) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }
        #endregion
    }
}
