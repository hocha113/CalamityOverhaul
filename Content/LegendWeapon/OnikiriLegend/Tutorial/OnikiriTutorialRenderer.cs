using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>鬼切教程卡片与焦点层</summary>
    internal static class OnikiriTutorialRenderer
    {
        private enum ButtonAction : byte
        {
            None,
            Primary,
            Secondary,
        }

        private const int CardWidth = 352;
        private const int EdgePad = 10;
        private const float TitleScale = 0.9f;
        private const float BodyScale = 0.78f;
        private const float PromptScale = 0.82f;
        private const float HintScale = 0.72f;
        private const float ContentPadX = 16f;
        private const float ContentPadTop = 13f;

        private static float cardAnimation;
        private static float shaderTime;
        private static int lastStep = -1;
        private static int layoutStep = -1;
        private static Rectangle cardRect = Rectangle.Empty;
        private static Rectangle primaryRect = Rectangle.Empty;
        private static Rectangle secondaryRect = Rectangle.Empty;
        private static ButtonAction primaryAction;
        private static ButtonAction secondaryAction;

        internal static void Reset()
        {
            cardAnimation = 0f;
            shaderTime = 0f;
            lastStep = -1;
            layoutStep = -1;
            ClearHitboxes();
        }

        private readonly struct GuideLine
        {
            internal readonly string Text;
            internal readonly float Scale;
            internal readonly Color Color;

            internal GuideLine(string text, float scale, Color color)
            {
                Text = text;
                Scale = scale;
                Color = color;
            }
        }

        internal static void UpdateInput()
        {
            bool mouseDown = Main.mouseLeft;
            bool clicked = OnikiriTutorialFlow.PollTutorialUiClick(mouseDown);

            if (!OnikiriTutorialFlow.IsRunning || layoutStep != OnikiriTutorialFlow.CurrentStep) {
                ClearHitboxes();
                return;
            }

            Point mouse = OnikiriUITheme.UIMouse.ToPoint();
            bool overCard = cardRect.Contains(mouse);
            bool overPrimary = primaryAction != ButtonAction.None && primaryRect.Contains(mouse);
            bool overSecondary = secondaryAction != ButtonAction.None && secondaryRect.Contains(mouse);
            if (overCard || overPrimary || overSecondary) {
                Main.LocalPlayer.mouseInterface = true;
            }
            if (!clicked || !overCard && !overPrimary && !overSecondary) {
                return;
            }

            Main.mouseLeft = false;
            Main.mouseLeftRelease = false;
            if (overPrimary) {
                OnikiriTutorialFlow.HandlePrimaryAction();
            }
            else if (overSecondary) {
                OnikiriTutorialFlow.HandleSecondaryAction();
            }
        }

        internal static void Draw()
        {
            if (!OnikiriTutorialFlow.IsRunning) {
                cardAnimation = 0f;
                lastStep = -1;
                ClearHitboxes();
                return;
            }

            int step = OnikiriTutorialFlow.CurrentStep;
            if (step != lastStep) {
                cardAnimation = 0f;
                lastStep = step;
                ClearHitboxes();
            }
            cardAnimation = MathHelper.Lerp(cardAnimation, 1f, 0.12f);
            shaderTime += 0.016f;
            if (cardAnimation < 0.02f) {
                return;
            }

            SpriteBatch spriteBatch = Main.spriteBatch;
            float time = Main.GlobalTimeWrappedHourly;
            HudFocusSnapshot focus = ResolveFocus(step);
            if (focus != null && step is not OnikiriTutorialFlow.Step_Dismember
                and not OnikiriTutorialFlow.Step_Backlash) {
                DrawHighlightRect(spriteBatch, focus.Rect, time, cardAnimation);
            }
            DrawStepCard(spriteBatch, step, focus, time, cardAnimation);
        }

        private static HudFocusSnapshot ResolveFocus(int step)
        {
            string tag = step switch {
                OnikiriTutorialFlow.Step_HudIntro => OnikiriTutorialTargets.Tag_VigorStroke,
                OnikiriTutorialFlow.Step_Mei => OniMeiUI.Instance?.IsOpen == true
                    ? OnikiriTutorialTargets.Tag_MeiSlotNakago
                    : OnikiriTutorialTargets.Tag_TalismanStrip,
                OnikiriTutorialFlow.Step_Register => OniRegisterUI.Instance?.IsOpen == true
                    ? OnikiriTutorialTargets.Tag_RegisterEntry
                    : OniMeiUI.Instance?.IsOpen == true
                        ? OnikiriTutorialTargets.Tag_RegisterSwitch
                        : OnikiriTutorialTargets.Tag_TalismanStrip,
                OnikiriTutorialFlow.Step_CloseEye => OnikiriTutorialTargets.Tag_DomainEye,
                _ => null,
            };
            if (tag != null) {
                return OnikiriTutorialTargets.Get(tag);
            }
            if (step is OnikiriTutorialFlow.Step_Dismember or OnikiriTutorialFlow.Step_Backlash) {
                return GetWorldTargetFocus(OnikiriTutorialFlow.TutorialTarget);
            }
            return null;
        }

        private static HudFocusSnapshot GetWorldTargetFocus(NPC target)
        {
            if (target?.active != true) {
                return null;
            }
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) zoom.X = 1f;
            if (zoom.Y <= 0f) zoom.Y = 1f;
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 screenCenter = screenSize * 0.5f;
            Vector2 viewCenter = Main.screenPosition + screenCenter;
            Vector2 screenPosition = screenCenter + (target.Center - viewCenter) * zoom;
            Vector2 uiCenter = screenPosition / Main.UIScale;
            Vector2 uiSize = target.Size * zoom / Main.UIScale;
            Rectangle rect = new((int)(uiCenter.X - uiSize.X * 0.5f),
                (int)(uiCenter.Y - uiSize.Y * 0.5f),
                Math.Max((int)uiSize.X, 1), Math.Max((int)uiSize.Y, 1));
            rect.Inflate(16, 16);
            return new HudFocusSnapshot {
                Frame = Main.GameUpdateCount,
                Rect = rect,
                Tag = "tutorial_santa",
            };
        }

        private static void DrawStepCard(SpriteBatch spriteBatch, int step, HudFocusSnapshot focus,
            float time, float alpha)
        {
            if (!TryGetStepCopy(step, out LocalizedText title, out LocalizedText body,
                out LocalizedText prompt)) {
                ClearHitboxes();
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float contentWidth = CardWidth - ContentPadX * 2f;
            List<GuideLine> lines = [
                new(body.Value, BodyScale, OnikiriUITheme.TextDim),
            ];
            string promptText = FormatPrompt(step, prompt);
            if (!string.IsNullOrEmpty(promptText)) {
                lines.Add(new GuideLine(promptText, PromptScale, OnikiriUITheme.HotWhite));
            }
            string hint = GetContextHint(step);
            if (!string.IsNullOrEmpty(hint)) {
                Color hintColor = OnikiriTutorialFlow.Feedback == OnikiriTutorialFeedback.Retry
                    ? OnikiriUITheme.Seal : OnikiriUITheme.GoldInlay;
                lines.Add(new GuideLine(hint, HintScale, hintColor));
            }

            int cardHeight = MeasureCardHeight(font, lines, contentWidth);
            Rectangle card = PlaceCard(focus, cardHeight, alpha);
            if (focus != null) {
                DrawConnector(spriteBatch, card, focus.Rect.Center.ToVector2(), alpha, time);
            }
            DrawCardPanel(spriteBatch, card, alpha, time);
            DrawCardContent(spriteBatch, font, card, title.Value, lines, alpha);
            BuildAndDrawButtons(spriteBatch, font, card, step, time, alpha);

            cardRect = card;
            layoutStep = step;
        }

        private static bool TryGetStepCopy(int step, out LocalizedText title,
            out LocalizedText body, out LocalizedText prompt)
        {
            title = body = prompt = null;
            switch (step) {
                case OnikiriTutorialFlow.Step_HudIntro:
                    title = OnikiriTutorialLead.HudTitle;
                    body = OnikiriTutorialLead.HudBody;
                    prompt = OnikiriTutorialLead.HudPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Mei:
                    title = OnikiriTutorialLead.MeiTitle;
                    body = OnikiriTutorialLead.MeiBody;
                    prompt = OnikiriTutorialLead.MeiPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Register:
                    title = OnikiriTutorialLead.RegisterTitle;
                    body = OnikiriTutorialLead.RegisterBody;
                    prompt = OnikiriTutorialLead.RegisterPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Prepare:
                    title = OnikiriTutorialLead.PrepareTitle;
                    body = OnikiriTutorialLead.PrepareBody;
                    prompt = OnikiriTutorialLead.PreparePrompt;
                    break;
                case OnikiriTutorialFlow.Step_OpenOmote:
                    title = OnikiriTutorialLead.OpenDomainTitle;
                    body = OnikiriTutorialLead.OpenDomainBody;
                    prompt = OnikiriTutorialLead.OpenDomainPrompt;
                    break;
                case OnikiriTutorialFlow.Step_FlipUra:
                    title = OnikiriTutorialLead.FlipDomainTitle;
                    body = OnikiriTutorialLead.FlipDomainBody;
                    prompt = OnikiriTutorialLead.FlipDomainPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Dismember:
                    title = OnikiriTutorialLead.DismemberTitle;
                    body = OnikiriTutorialLead.DismemberBody;
                    prompt = OnikiriTutorialLead.DismemberPrompt;
                    break;
                case OnikiriTutorialFlow.Step_Backlash:
                    title = OnikiriTutorialLead.BacklashTitle;
                    body = OnikiriTutorialLead.BacklashBody;
                    prompt = OnikiriTutorialLead.BacklashPrompt;
                    break;
                case OnikiriTutorialFlow.Step_CloseEye:
                    title = OnikiriTutorialLead.CloseDomainTitle;
                    body = OnikiriTutorialLead.CloseDomainBody;
                    prompt = OnikiriTutorialLead.CloseDomainPrompt;
                    break;
            }
            return title != null && body != null;
        }

        private static string FormatPrompt(int step, LocalizedText prompt)
        {
            if (prompt == null || string.IsNullOrEmpty(prompt.Value)) {
                return null;
            }
            string key = step switch {
                OnikiriTutorialFlow.Step_Mei => CWRKeySystem.GetKeybindText(
                    CWRKeySystem.Legend_UIControl, CWRKeySystem.Notbound.Value),
                OnikiriTutorialFlow.Step_OpenOmote => CWRKeySystem.GetKeybindText(
                    CWRKeySystem.Legend_Domain, "Q"),
                OnikiriTutorialFlow.Step_FlipUra => CWRKeySystem.GetKeybindText(
                    CWRKeySystem.Onikiri_DomainFlip, "Mouse3"),
                _ => null,
            };
            return key != null && prompt.Value.Contains("{0}")
                ? string.Format(prompt.Value, key)
                : prompt.Value;
        }

        private static string GetContextHint(int step)
        {
            string feedback = OnikiriTutorialFlow.Feedback switch {
                OnikiriTutorialFeedback.Waiting => OnikiriTutorialLead.WaitingFeedback.Value,
                OnikiriTutorialFeedback.Busy => OnikiriTutorialLead.BusyFeedback.Value,
                OnikiriTutorialFeedback.Retry => OnikiriTutorialLead.RetryFeedback.Value,
                _ => null,
            };
            if (!string.IsNullOrEmpty(feedback)) {
                return feedback;
            }
            if (step == OnikiriTutorialFlow.Step_OpenOmote
                && CWRKeySystem.IsKeybindUnbound(CWRKeySystem.Legend_Domain)) {
                return OnikiriTutorialLead.DomainUnboundHint.Value;
            }
            if (step == OnikiriTutorialFlow.Step_FlipUra
                && CWRKeySystem.IsKeybindUnbound(CWRKeySystem.Onikiri_DomainFlip)) {
                return OnikiriTutorialLead.FlipUnboundHint.Value;
            }
            return step == OnikiriTutorialFlow.Step_Dismember
                && OnikiriTutorialFlow.TutorialTarget == null
                ? OnikiriTutorialLead.WaitingFeedback.Value
                : null;
        }

        private static void BuildAndDrawButtons(SpriteBatch spriteBatch, DynamicSpriteFont font,
            Rectangle card, int step, float time, float alpha)
        {
            primaryRect = secondaryRect = Rectangle.Empty;
            primaryAction = secondaryAction = ButtonAction.None;

            string primaryText = null;
            string secondaryText = null;
            if (step == OnikiriTutorialFlow.Step_HudIntro) {
                primaryText = OnikiriTutorialLead.NextBtn.Value;
            }
            else if (step == OnikiriTutorialFlow.Step_Mei) {
                if (!(OniMeiUI.Instance?.IsOpen ?? false)) {
                    primaryText = OnikiriTutorialLead.OpenMeiBtn.Value;
                }
                if (OnikiriTutorialFlow.StepTimer >= OnikiriTutorialFlow.LegacySkipDelayFrames) {
                    secondaryText = OnikiriTutorialLead.SkipBtn.Value;
                }
            }
            else if (step == OnikiriTutorialFlow.Step_Register) {
                bool registerOpen = OniRegisterUI.Instance?.IsOpen ?? false;
                bool meiOpen = OniMeiUI.Instance?.IsOpen ?? false;
                if (registerOpen) {
                    primaryText = OnikiriTutorialLead.NextBtn.Value;
                }
                else if (!meiOpen) {
                    primaryText = OnikiriTutorialLead.OpenMeiBtn.Value;
                }
                if (!registerOpen && OnikiriTutorialFlow.StepTimer >= OnikiriTutorialFlow.LegacySkipDelayFrames) {
                    secondaryText = OnikiriTutorialLead.OpenRegisterBtn.Value;
                }
            }
            else if ((step is OnikiriTutorialFlow.Step_OpenOmote
                or OnikiriTutorialFlow.Step_FlipUra
                or OnikiriTutorialFlow.Step_Dismember
                or OnikiriTutorialFlow.Step_CloseEye)
                ) {
                if (OnikiriTutorialFlow.Feedback == OnikiriTutorialFeedback.Retry) {
                    primaryText = OnikiriTutorialLead.RetryBtn.Value;
                }
                if (OnikiriTutorialFlow.StepTimer >= OnikiriTutorialFlow.AssistDelayFrames) {
                    secondaryText = OnikiriTutorialLead.AssistBtn.Value;
                }
            }

            if (!string.IsNullOrEmpty(primaryText)) {
                primaryRect = MakeButtonRect(font, card, primaryText, rightAligned: true, 24, 0.76f);
                primaryAction = ButtonAction.Primary;
                DrawPaperButton(spriteBatch, font, primaryRect, primaryText,
                    OnikiriUITheme.Bright, time, alpha, 0.76f);
            }
            if (!string.IsNullOrEmpty(secondaryText)) {
                secondaryRect = MakeButtonRect(font, card, secondaryText, rightAligned: false, 22, 0.7f);
                secondaryAction = ButtonAction.Secondary;
                DrawPaperButton(spriteBatch, font, secondaryRect, secondaryText,
                    OnikiriUITheme.GhostFire, time, alpha * 0.92f, 0.7f);
            }
        }

        private static Rectangle MakeButtonRect(DynamicSpriteFont font, Rectangle card, string text,
            bool rightAligned, int height, float textScale)
        {
            int width = Math.Max(rightAligned ? 98 : 84,
                (int)(font.MeasureString(text).X * textScale) + 24);
            int x = rightAligned ? card.Right - width - 12 : card.X + 12;
            return new Rectangle(x, card.Bottom - height - 11, width, height);
        }

        private static void DrawHighlightRect(SpriteBatch spriteBatch, Rectangle rect,
            float time, float alpha)
        {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) return;
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 2.4f));
            Rectangle outer = rect;
            outer.Inflate(5, 5);
            OniBrush.DrawBacklight(spriteBatch, outer.Center.ToVector2(),
                MathF.Max(outer.Width, outer.Height) * 0.55f,
                OnikiriUITheme.Bright, alpha * 0.22f * pulse);
            DrawDashedBorder(spriteBatch, pixel, outer,
                OnikiriUITheme.Bright * ((0.55f + pulse * 0.35f) * alpha),
                6f, 4f, time * -22f);
        }

        private static void DrawDashedBorder(SpriteBatch spriteBatch, Texture2D pixel,
            Rectangle rect, Color color, float dash, float gap, float flow)
        {
            DrawDashedSegment(spriteBatch, pixel, new Vector2(rect.Left, rect.Top),
                new Vector2(rect.Right, rect.Top), color, dash, gap, flow);
            DrawDashedSegment(spriteBatch, pixel, new Vector2(rect.Right, rect.Top),
                new Vector2(rect.Right, rect.Bottom), color, dash, gap, flow);
            DrawDashedSegment(spriteBatch, pixel, new Vector2(rect.Right, rect.Bottom),
                new Vector2(rect.Left, rect.Bottom), color, dash, gap, flow);
            DrawDashedSegment(spriteBatch, pixel, new Vector2(rect.Left, rect.Bottom),
                new Vector2(rect.Left, rect.Top), color, dash, gap, flow);
        }

        private static void DrawDashedSegment(SpriteBatch spriteBatch, Texture2D pixel,
            Vector2 from, Vector2 to, Color color, float dash, float gap, float flow)
        {
            Vector2 edge = to - from;
            float length = edge.Length();
            if (length < 1f) return;
            Vector2 direction = edge / length;
            float rotation = direction.ToRotation();
            float period = dash + gap;
            float offset = ((flow % period) + period) % period;
            for (float distance = -offset; distance < length; distance += period) {
                float start = Math.Max(0f, distance);
                float end = Math.Min(length, distance + dash);
                if (end <= start) continue;
                spriteBatch.Draw(pixel, from + direction * start, new Rectangle(0, 0, 1, 1),
                    color, rotation, new Vector2(0f, 0.5f),
                    new Vector2(end - start, 1.6f), SpriteEffects.None, 0f);
            }
        }

        private static Rectangle PlaceCard(HudFocusSnapshot focus, int cardHeight, float alpha)
        {
            float ease = VaultUtils.EaseOutCubic(alpha);
            float slide = (1f - ease) * 28f;
            float screenWidth = OnikiriUITheme.UIScreenW;
            float screenHeight = OnikiriUITheme.UIScreenH;
            float x;
            float y;
            if (focus != null) {
                x = focus.Rect.Right + 18f - slide;
                if (x + CardWidth > screenWidth - 16f) {
                    x = focus.Rect.Left - CardWidth - 18f + slide;
                }
                y = focus.Rect.Center.Y - cardHeight * 0.5f;
            }
            else {
                x = screenWidth - CardWidth - 24f;
                y = screenHeight * 0.32f;
            }
            x = MathHelper.Clamp(x, 16f, screenWidth - CardWidth - 16f);
            y = MathHelper.Clamp(y, 16f, screenHeight - cardHeight - 16f);
            return new Rectangle((int)x, (int)y, CardWidth, cardHeight);
        }

        private static void DrawCardPanel(SpriteBatch spriteBatch, Rectangle card,
            float alpha, float time)
        {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            OniBrush.DrawPanelDropShadow(spriteBatch, card.Center.ToVector2(),
                new Vector2(card.Width, card.Height), alpha);
            if (OniShaderPanel.Available) {
                OniShaderPanel.Draw(spriteBatch, card, Math.Min(1f, alpha * 1.35f),
                    MathHelper.Lerp(0.82f, 1f, alpha), shaderTime, EdgePad, Color.White);
            }
            else {
                spriteBatch.Draw(pixel, card, new Rectangle(0, 0, 1, 1),
                    OnikiriUITheme.Ink * (alpha * 0.96f));
                SkinDrawUtil.DrawRectBorder(spriteBatch, card,
                    OnikiriUITheme.Deep * (alpha * 0.65f), 1);
            }
            OniBrush.DrawTaperedSlash(spriteBatch,
                new Vector2(card.X + 10f, card.Y + 1f),
                new Vector2(card.Right - 10f, card.Y + 2f), 1.5f, 0.6f, alpha * 0.55f);
            OniBrush.DrawShide(spriteBatch, card, alpha * 0.85f, time);
            OniBrush.DrawSealGlyph(spriteBatch,
                new Vector2(card.X + ContentPadX + 6f, card.Y + ContentPadTop + 10f),
                11f, alpha * 0.95f, time * 0.02f);
        }

        private static void DrawCardContent(SpriteBatch spriteBatch, DynamicSpriteFont font,
            Rectangle card, string title, List<GuideLine> lines, float alpha)
        {
            float x = card.X + ContentPadX;
            float y = card.Y + ContentPadTop;
            float wrap = card.Width - ContentPadX * 2f;
            float titleX = x + 20f;
            Utils.DrawBorderString(spriteBatch, title, new Vector2(titleX + 1f, y + 1f),
                Color.Black * (0.45f * alpha), TitleScale);
            Utils.DrawBorderString(spriteBatch, title, new Vector2(titleX, y),
                OnikiriUITheme.HotWhite * alpha, TitleScale);
            y += font.MeasureString("A").Y * TitleScale + 8f;
            OniBrush.DrawTaperedSlash(spriteBatch, new Vector2(x, y),
                new Vector2(card.Right - ContentPadX, y - 1f), 1.6f, 0.9f, alpha * 0.85f);
            y += 8f;
            foreach (GuideLine line in lines) {
                y = DrawBody(spriteBatch, font, line.Text, x, y, wrap,
                    line.Scale, line.Color, alpha) + 4f;
            }
        }

        private static float DrawBody(SpriteBatch spriteBatch, DynamicSpriteFont font,
            string text, float x, float y, float wrapWidth, float scale, Color color, float alpha)
        {
            if (string.IsNullOrEmpty(text)) return y;
            string[] wrapped = CWRUtils.WrapTextArray(text, font,
                Math.Max(8, (int)(wrapWidth / scale)), 99, out _);
            float lineHeight = font.MeasureString("A").Y * scale + 3f;
            foreach (string line in wrapped) {
                if (string.IsNullOrEmpty(line)) continue;
                Utils.DrawBorderString(spriteBatch, line, new Vector2(x + 1f, y + 1f),
                    Color.Black * (0.5f * alpha), scale);
                Utils.DrawBorderString(spriteBatch, line, new Vector2(x, y), color * alpha, scale);
                y += lineHeight;
            }
            return y;
        }

        private static int MeasureCardHeight(DynamicSpriteFont font,
            List<GuideLine> lines, float contentWidth)
        {
            float height = ContentPadTop + font.MeasureString("A").Y * TitleScale + 16f;
            foreach (GuideLine line in lines) {
                int count = CWRUtils.WrapTextArray(line.Text, font,
                    Math.Max(8, (int)(contentWidth / line.Scale)), 99, out _)
                    .Count(value => !string.IsNullOrEmpty(value));
                height += Math.Max(count, 1) * (font.MeasureString("A").Y * line.Scale + 3f) + 4f;
            }
            return (int)MathF.Ceiling(height + 43f);
        }

        private static void DrawConnector(SpriteBatch spriteBatch, Rectangle card,
            Vector2 target, float alpha, float time)
        {
            Vector2 from = card.Center.X < target.X
                ? new Vector2(card.Right - 4f, card.Center.Y)
                : new Vector2(card.X + 4f, card.Center.Y);
            OniBrush.DrawGradientLine(spriteBatch, from, target,
                OnikiriUITheme.Bright * (0.55f * alpha),
                OnikiriUITheme.Deep * (0.08f * alpha), 1.3f);
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 3.2f);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, target, new Rectangle(0, 0, 1, 1),
                OnikiriUITheme.HotWhite * (0.7f * alpha * pulse), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(5.5f), SpriteEffects.None, 0f);
        }

        private static void DrawPaperButton(SpriteBatch spriteBatch, DynamicSpriteFont font,
            Rectangle rect, string text, Color accent, float time, float alpha, float textScale)
        {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            bool hovered = rect.Contains(OnikiriUITheme.UIMouse.ToPoint());
            float highlight = hovered ? 1f : 0f;
            Color fill = Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark,
                0.35f + highlight * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width, rect.Height),
                new Rectangle(0, 0, 1, 1), new Color(8, 2, 5) * (alpha * 0.35f));
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), fill * (alpha * 0.94f));
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect,
                accent * ((0.45f + highlight * 0.35f) * alpha), 1);
            if (hovered) {
                float sweep = (time * 0.9f) % 1.2f / 1.2f;
                float x = MathHelper.Lerp(rect.X + 4f, rect.Right - 4f, sweep);
                OniBrush.DrawSoftStreak(spriteBatch, new Vector2(x, rect.Center.Y), -0.9f,
                    rect.Height * 0.85f, 1.4f, OnikiriUITheme.HotWhite, alpha * 0.35f, 0.6f);
            }
            Vector2 size = font.MeasureString(text) * textScale;
            Vector2 position = rect.Center.ToVector2() - size * 0.5f + new Vector2(0f, -1f);
            Utils.DrawBorderString(spriteBatch, text, position,
                Color.Lerp(OnikiriUITheme.Paper, accent, 0.25f + highlight * 0.45f) * alpha,
                textScale);
        }

        private static void ClearHitboxes()
        {
            cardRect = primaryRect = secondaryRect = Rectangle.Empty;
            primaryAction = secondaryAction = ButtonAction.None;
            layoutStep = -1;
        }
    }
}
