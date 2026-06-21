using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using InnoVault.Narrative.Styling;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.StarStream
{
    internal sealed class StarStreamDialogueSkin : StoryDialogueSkin
    {
        private readonly StarStreamPanelState state = new();

        public override float Padding => 10;
        public override float TextWrapInset => StarStreamPanelState.TextWrapInset;

        public override Color TextColor => new(255, 245, 220);
        public override Color SpeakerColor => new(255, 225, 135);
        public override Color HintColor => new(255, 210, 100);
        public override Color SilhouetteColor => new Color(15, 10, 30) * 0.85f;

        public override void Update(DialogueLayoutContext context)
            => state.UpdateDialogue(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => StarStreamPanelDraw.DrawDialogueBackground(spriteBatch, context.PanelRect, context.Alpha, state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => state.DrawStreamParticles(spriteBatch, context.Alpha);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;

            spriteBatch.Draw(px, frame, new Rectangle(0, 0, 1, 1), new Color(10, 8, 22) * (alpha * 0.9f));

            Color edge = new Color(220, 180, 80) * (alpha * 0.6f);
            spriteBatch.Draw(px, new Rectangle(frame.X, frame.Y, frame.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(frame.X, frame.Bottom - 2, frame.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(px, new Rectangle(frame.X, frame.Y, 2, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            spriteBatch.Draw(px, new Rectangle(frame.Right - 2, frame.Y, 2, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            float pulse = (float)Math.Sin(state.NebulaPulseTimer * 1.2f) * 0.5f + 0.5f;
            Rectangle glow = frame;
            glow.Inflate(3, 3);
            Color starRim = new Color(255, 200, 100) * (context.Alpha * 0.45f * pulse);
            StarStreamPanelDraw.DrawStarGlowRect(spriteBatch, glow, starRim);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float nameAlpha = context.ContentAlpha * context.SpeakerSwitchEase;
            Vector2 speakerPos = context.SpeakerRect.Location.ToVector2();
            speakerPos.Y -= (1f - context.SpeakerSwitchEase) * 6f;

            Color nameGlow = new Color(255, 210, 120) * nameAlpha * 0.7f;
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + state.ShimmerTimer * 0.3f;
                Vector2 offset = angle.ToRotationVector2() * 2.2f * context.SpeakerSwitchEase;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, speakerPos + offset, nameGlow * 0.5f, NameScale);
            }

            Utils.DrawBorderString(spriteBatch, context.SpeakerName, speakerPos, SpeakerColor * nameAlpha, NameScale);

            Vector2 divStart = speakerPos + new Vector2(0, 26);
            Vector2 divEnd = new(context.SpeakerRect.Right, divStart.Y);
            DrawDividerLine(spriteBatch, divStart, divEnd, nameAlpha);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            DrawDividerLine(spriteBatch, start, end, context.ContentAlpha);
        }

        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            int remaining = context.VisibleChars;
            for (int i = 0; i < context.WrappedLines.Length; i++) {
                string fullLine = context.WrappedLines[i];
                string draw = fullLine;
                if (remaining < fullLine.Length) {
                    draw = fullLine[..Math.Max(0, remaining)];
                }
                if (draw.Length > 0) {
                    float drift = (float)Math.Sin(state.StarFlowTimer * 1.5f + i * 0.5f) * 0.7f;
                    Vector2 pos = new(context.TextRect.X + drift, context.TextRect.Y + i * context.LineHeight);

                    Color textGlow = new Color(255, 200, 80) * (context.ContentAlpha * 0.08f);
                    Utils.DrawBorderString(spriteBatch, draw, pos + new Vector2(0, 1), textGlow, TextScale);
                    Color lineColor = Color.Lerp(new Color(255, 245, 220), new Color(255, 255, 245), 0.3f) * context.ContentAlpha;
                    Utils.DrawBorderString(spriteBatch, draw, pos, lineColor, TextScale);
                }
                remaining -= fullLine.Length;
                if (remaining <= 0) {
                    break;
                }
            }
        }

        public override void DrawTimedIndicator(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (!context.TimedActive) {
                return;
            }

            int barWidth = (int)(context.PanelRect.Width * context.TimedProgress);
            Rectangle bar = new(context.PanelRect.X, context.PanelRect.Y - 4, barWidth, 3);
            Color baseColor = new Color(255, 210, 100);
            Color warningColor = new Color(255, 160, 60);
            Color dangerColor = new Color(255, 90, 60);
            Color color = context.TimedProgress > 0.66f
                ? Color.Lerp(warningColor, dangerColor, (context.TimedProgress - 0.66f) / 0.34f)
                : Color.Lerp(baseColor, warningColor, context.TimedProgress / 0.66f);
            NarrativeSkinDraw.FillRect(spriteBatch, bar, color * context.Alpha);
        }

        public override void DrawCommandHints(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (!context.ShowHints) {
                return;
            }

            Color on = HintColor * context.ContentAlpha;
            Color off = new Color(200, 175, 120) * (0.45f * context.ContentAlpha);
            Utils.DrawBorderString(spriteBatch, ResolveAutoHint(), GetHintDrawPosition(context, context.AutoRect, ResolveAutoHint()), context.AutoMode ? on : off, HintScale);
            Utils.DrawBorderString(spriteBatch, ResolveFastHint(), GetHintDrawPosition(context, context.FastRect, ResolveFastHint()), context.FastMode ? on : off, HintScale);
            Utils.DrawBorderString(spriteBatch, ResolveSkipHint(), GetHintDrawPosition(context, context.SkipRect, ResolveSkipHint()), off, HintScale);

            if (context.WaitingAdvance) {
                float blink = (float)(Math.Sin(context.GlobalTimer * 6f) * 0.5 + 0.5);
                string label = context.HoverContinue
                    ? $"\u2726 {ResolveContinueHint(true)} \u2726"
                    : $"\u2726 {ResolveContinueHint(false)} \u2726";
                Utils.DrawBorderString(spriteBatch, label, GetHintDrawPosition(context, context.ContinueRect, label, 0.9f), HintColor * (context.ContentAlpha * blink), 0.9f);
            }
        }

        private static void DrawDividerLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,
                new Color(220, 180, 80) * (alpha * 0.85f),
                new Color(220, 180, 80) * (alpha * 0.06f),
                1.5f);
        }
    }
}
