using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea
{
    internal sealed class SulfseaDialogueSkin : StoryDialogueSkin
    {
        private readonly SulfseaPanelState _state = new();

        public override Color TextColor => new(220, 230, 180);
        public override Color SpeakerColor => new(170, 205, 95);
        public override Color HintColor => new(160, 190, 80);
        public override Color SilhouetteColor => new Color(20, 30, 15) * 0.85f;

        public override void Update(DialogueLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => SulfseaPanelDraw.DrawPanel(spriteBatch, context.PanelRect, context.Alpha, _state.ToxicWavePhase, _state.SulfurPulse, _state.MiasmaTimer);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;
            spriteBatch.Draw(pixel, frame, new Rectangle(0, 0, 1, 1), new Color(10, 15, 8) * (alpha * 0.88f));
            SkinDrawUtil.DrawRectBorder(spriteBatch, frame, new Color(100, 130, 50) * (alpha * 0.75f), 2);
            Rectangle glow = frame;
            glow.Inflate(4, 4);
            SkinDrawUtil.DrawGlowRect(spriteBatch, glow, new Color(140, 180, 70) * (alpha * 0.28f));
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            Vector2 pos = context.SpeakerRect.Location.ToVector2();
            Color glow = new Color(160, 190, 80) * (context.Alpha * 0.75f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos + angle.ToRotationVector2() * 1.8f, glow * 0.6f, NameScale);
            }
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, pos, SpeakerColor * context.Alpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            SkinDrawUtil.DrawGradientLine(spriteBatch, start, end,
                new Color(100, 140, 50) * (context.Alpha * 0.9f),
                new Color(100, 140, 50) * (context.Alpha * 0.08f),
                1.3f);
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
                    float wobble = (float)Math.Sin(_state.ToxicWavePhase * 2.4f + i * 0.6f) * 1.3f;
                    Vector2 pos = new(context.TextRect.X + wobble, context.TextRect.Y + i * context.LineHeight);
                    Utils.DrawBorderString(spriteBatch, draw, pos, TextColor * context.Alpha, TextScale);
                }
                remaining -= fullLine.Length;
                if (remaining <= 0) {
                    break;
                }
            }
        }
    }
}
