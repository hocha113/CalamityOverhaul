using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using InnoVault.Narrative.Presentation.Dialogue;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Draedon
{
    internal sealed class DraedonDialogueSkin : StoryDialogueSkin
    {
        private readonly DraedonPanelState state = new();

        public override float PanelWidth => 540f;
        public override Color TextColor => Color.Lerp(new Color(205, 245, 255), Color.White, 0.2f);
        public override Color SpeakerColor => new(0, 220, 200);
        public override Color HintColor => new(0, 210, 185);
        public override Color SilhouetteColor => new Color(20, 35, 55) * 0.85f;

        public override void Update(DialogueLayoutContext context)
            => state.Update(context.PanelRect, context.Alpha > 0.04f);

        public override void Reset() => state.Reset();

        public override void DrawBackground(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => DraedonPanelDraw.DrawPanel(spriteBatch, context.PanelRect, context.Alpha, state);

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, DialogueLayoutContext context)
            => state.DrawParticles(spriteBatch, context.Alpha, 0.78f, 0.68f);

        public override void DrawPortraitFrame(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (context.PortraitRect == Rectangle.Empty) {
                return;
            }
            DraedonPanelDraw.DrawPortraitFrame(spriteBatch, context.PortraitRect, context.Alpha, state.CircuitPulseTimer);
        }

        public override void DrawSpeakerName(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            if (string.IsNullOrEmpty(context.SpeakerName)) {
                return;
            }

            DraedonPanelDraw.DrawSpeakerGlow(spriteBatch, context.SpeakerRect.Location.ToVector2(),
                context.SpeakerName, context.Alpha, NameScale);
            Utils.DrawBorderString(spriteBatch, context.SpeakerName, context.SpeakerRect.Location.ToVector2(),
                SpeakerColor * context.Alpha, NameScale);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, DialogueLayoutContext context) {
            Vector2 start = new(context.SpeakerRect.X, context.SpeakerRect.Bottom - 3);
            Vector2 end = new(context.SpeakerRect.Right, context.SpeakerRect.Bottom - 3);
            DraedonPanelDraw.DrawDashDivider(spriteBatch, start, end, context.Alpha, state.DataStreamTimer);
        }
    }
}
