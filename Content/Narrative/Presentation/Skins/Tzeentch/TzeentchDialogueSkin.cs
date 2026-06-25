using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Tzeentch
{
    internal sealed class TzeentchDialogueSkin : StoryDialogueSkin
    {
        private readonly TzeentchPanelState _state = new();

        public override Color TextColor => new(228, 222, 250);
        public override Color SpeakerColor => TzeentchPalette.Gold;
        public override Color HintColor => new(190, 160, 235);
        public override Color SilhouetteColor => new Color(18, 10, 32) * 0.9f;

        public override float TextWrapInset => TzeentchPanelState.ShaderEdgePad;

        public override void Update(DialogueLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => _state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            TzeentchPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);
            TzeentchPanelDraw.DrawCornerSigils(spriteBatch, context.PanelRect, context.Alpha * 0.9f);
        }

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => _state.DrawForeground(spriteBatch, context.Alpha);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (context.PortraitRect == Rectangle.Empty) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle frame = context.PortraitRect;
            float alpha = context.Alpha;
            spriteBatch.Draw(pixel, frame, new Rectangle(0, 0, 1, 1), new Color(12, 7, 24) * (alpha * 0.9f));
            float pulse = (float)Math.Sin(_state.SchemePulse * 2.2f) * 0.5f + 0.5f;
            SkinDrawUtil.DrawRectBorder(spriteBatch, frame, Color.Lerp(TzeentchPalette.Violet, TzeentchPalette.Gold, pulse) * (alpha * 0.78f), 2);
            Rectangle glow = frame;
            glow.Inflate(4, 4);
            SkinDrawUtil.DrawGlowRect(spriteBatch, glow, TzeentchPalette.Violet * (alpha * 0.3f));
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            Vector2 pos = context.SpeakerRect.Location.ToVector2();
            Color glow = TzeentchPalette.Gold * (context.Alpha * 0.7f);
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
                TzeentchPalette.Gold * (context.Alpha * 0.9f),
                TzeentchPalette.Gold * (context.Alpha * 0.06f),
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
                    //极轻的现实流变抖动,呼应"变数"主题且不妨碍阅读
                    float wobble = (float)Math.Sin(_state.WarpTimer * 2.0f + i * 0.7f) * 0.7f;
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
