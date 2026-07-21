using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Brimstone
{
    internal sealed class BrimstoneDialogueSkin : StoryDialogueSkin
    {
        private readonly BrimstonePanelState _state = new();

        public override float PortraitSize => 100;
        public override float Padding => 10;
        /// <summary>对齐 Brimstone shader 内缘</summary>
        public override float TextWrapInset => BrimstonePanelState.ShaderEdgePad;

        public override Color TextColor => new(255, 225, 210);
        public override Color SpeakerColor => new(255, 240, 220);
        public override Color HintColor => new(255, 160, 90);
        public override Color SilhouetteColor => new Color(40, 10, 5) * 0.85f;

        public override void Update(DialogueLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => BrimstonePanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => _state.DrawParticles(spriteBatch, context.Alpha);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;
            spriteBatch.Draw(pixel, frame, new Rectangle(0, 0, 1, 1), new Color(20, 5, 5) * (alpha * 0.88f));
            Color edge = new Color(200, 80, 40) * (alpha * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, frame.Width, 3), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Bottom - 3, frame.Width, 3), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, 3, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(frame.Right - 3, frame.Y, 3, frame.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            Rectangle glow = frame;
            glow.Inflate(4, 4);
            float pulse = (float)Math.Sin(_state.FlameTimer * 1.8f) * 0.5f + 0.5f;
            BrimstonePanelDraw.DrawFlameGlow(spriteBatch, glow, new Color(255, 120, 60) * (context.ContentAlpha * 0.5f * pulse));
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            float nameAlpha = context.ContentAlpha * context.SpeakerSwitchEase;
            Vector2 pos = context.SpeakerRect.Location.ToVector2();
            pos.Y -= (1f - context.SpeakerSwitchEase) * 6f;
            Color nameGlow = new Color(255, 140, 80) * (nameAlpha * 0.75f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + _state.FlameTimer * 0.5f;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos + angle.ToRotationVector2() * 2.2f * context.SpeakerSwitchEase, nameGlow * 0.5f, NameScale);
            }
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos, SpeakerColor * nameAlpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,
                new Color(220, 80, 40) * (context.ContentAlpha * 0.9f),
                new Color(120, 30, 15) * (context.ContentAlpha * 0.1f),
                1.5f);
        }

        public override void DrawText(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            int remaining = context.VisibleChars;
            for (int i = 0; i < context.WrappedLines.Length; i++) {
                string fullLine = context.WrappedLines[i];
                string draw = remaining < fullLine.Length ? fullLine[..Math.Max(0, remaining)] : fullLine;
                if (draw.Length > 0) {
                    float heatWobble = (float)Math.Sin(_state.FlameTimer * 3f + i * 0.65f) * 0.9f;
                    Vector2 pos = new(context.TextRect.X + heatWobble, context.TextRect.Y + i * context.LineHeight);
                    Color glow = new Color(255, 150, 80) * (context.ContentAlpha * 0.15f);
                    Utils.DrawBorderString(spriteBatch, draw, pos + new Vector2(0, 1), glow, TextScale);
                    Utils.DrawBorderString(spriteBatch, draw, pos, TextColor * context.ContentAlpha, TextScale);
                }
                remaining -= fullLine.Length;
                if (remaining <= 0) {
                    break;
                }
            }
        }
    }
}
