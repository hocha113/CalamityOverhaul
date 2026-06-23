using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.SHPC
{
    internal sealed class SHPCChoiceSkin : StoryChoiceSkin
    {
        private readonly SHPCPanelState _state = new();

        public override Color TextColor => new(210, 245, 255);
        public override Color DisabledTextColor => new(80, 100, 130);

        public override void Update(ChoiceLayoutContext context)
            => _state.Update(context.PanelRect, context.Alpha > 0.01f, dialogueDecorations: true);

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            SHPCPanelDraw.DrawCyberBackground(spriteBatch, context.PanelRect, context.Alpha, _state, layeredShadow: true);
            SHPCPanelDraw.DrawDialogueDecorations(spriteBatch, context.PanelRect, context.Alpha, _state);
        }

        public override void DrawBackgroundDecorations(SpriteBatch spriteBatch, ChoiceLayoutContext context)
            => _state.DrawParticles(spriteBatch, context.Alpha, dialogueDecorations: true);

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            Color glow = SHPCPanelState.NeonBlueColor * (context.Alpha * 0.7f);
            Vector2 titlePos = context.TitleRect.Location.ToVector2();
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Utils.DrawBorderString(spriteBatch, ResolveChoiceTitle(), titlePos + ang.ToRotationVector2() * 1.4f, glow * 0.5f, 0.85f);
            }
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color choiceBg = option.Enabled
                ? Color.Lerp(SHPCPanelState.PanelDarkColor * 0.35f, new Color(20, 12, 40) * 0.55f, hover)
                : new Color(8, 6, 16) * 0.15f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);
            if (option.Enabled && hover > 0.01f) {
                SkinDrawUtil.DrawRectBorder(spriteBatch, rect, SHPCPanelState.NeonBlueColor * (context.Alpha * hover * 0.55f), 1);
            }
        }
    }
}
