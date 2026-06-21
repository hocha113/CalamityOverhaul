using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sea
{
    internal sealed class SeaDialogueSkin : StoryDialogueSkin
    {
        private readonly SeaPanelState _state = new();

        public override float Padding => 10;

        public override float PortraitSize => 120f;
        public override float PortraitGap => 20f;
        /// <summary>对齐 Sea shader 内缘留白。</summary>
        public override float TextWrapInset => SeaPanelState.ShaderEdgePad;

        public override Color TextColor => new(210, 240, 255);
        public override Color SpeakerColor => new(150, 220, 255);
        public override Color HintColor => new(120, 200, 255);
        public override Color SilhouetteColor => new Color(10, 30, 40) * 0.9f;

        public override void Update(DialogueLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => SeaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;
            spriteBatch.Draw(pixel, frame, new Rectangle(0, 0, 1, 1), new Color(5, 20, 28) * (alpha * 0.85f));
            Color edge = new Color(70, 180, 230) * (alpha * 0.6f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, frame.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Bottom - 2, frame.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, 2, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(frame.Right - 2, frame.Y, 2, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);

            Rectangle glow = frame;
            glow.Inflate(4, 4);
            float pulse = (float)Math.Sin(_state.PanelPulse * 1.2f) * 0.5f + 0.5f;
            Color rim = new Color(140, 230, 255) * (context.ContentAlpha * 0.4f * pulse + context.ContentAlpha * 0.16f);
            SkinDrawUtil.DrawGlowRect(spriteBatch, glow, rim);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float nameAlpha = context.ContentAlpha * context.SpeakerSwitchEase;
            Vector2 pos = context.SpeakerRect.Location.ToVector2();
            pos.Y -= (1f - context.SpeakerSwitchEase) * 6f;
            Color nameGlow = new Color(140, 230, 255) * (nameAlpha * 0.7f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos + angle.ToRotationVector2() * 2.2f * context.SpeakerSwitchEase, nameGlow * 0.55f, NameScale);
            }
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos, SpeakerColor * nameAlpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,
                new Color(70, 180, 230) * (context.ContentAlpha * 0.85f),
                new Color(70, 180, 230) * (context.ContentAlpha * 0.05f),
                1.3f);
        }

        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            int remaining = context.VisibleChars;
            for (int i = 0; i < context.WrappedLines.Length; i++) {
                string fullLine = context.WrappedLines[i];
                string draw = remaining < fullLine.Length ? fullLine[..Math.Max(0, remaining)] : fullLine;
                if (draw.Length > 0) {
                    float wobble = (float)Math.Sin(_state.WavePhase * 2.2f + i * 0.55f) * 1.2f;
                    Vector2 pos = new(context.TextRect.X + wobble, context.TextRect.Y + i * context.LineHeight);
                    Color lineColor = Color.Lerp(new Color(180, 230, 250), Color.White, 0.35f) * context.ContentAlpha;
                    Utils.DrawBorderString(spriteBatch, draw, pos, lineColor, TextScale);
                }
                remaining -= fullLine.Length;
                if (remaining <= 0) {
                    break;
                }
            }
        }
    }
}
