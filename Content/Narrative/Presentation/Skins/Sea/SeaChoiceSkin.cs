using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Choices;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Sea
{
    internal sealed class SeaChoiceSkin : StoryChoiceSkin
    {
        private readonly SeaPanelState _state = new();
        private float _styleAnimTimer;

        public override Color TextColor => new(210, 240, 255);
        public override Color DisabledTextColor => new(90, 120, 140);

        public override void Update(ChoiceLayoutContext context) {
            _styleAnimTimer = SkinAnimUtil.WrapTimer(_styleAnimTimer, 0.05f);
            _state.Update(context.PanelRect, context.Alpha > 0.01f);
        }

        public override void Reset() {
            _state.Reset();
            _styleAnimTimer = 0f;
        }

        public override void DrawPanel(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            SeaPanelDraw.DrawShaderBackground(spriteBatch, context.PanelRect, context.Alpha, _state);
            Color edge = GetEdgeColor(context.Alpha);
            SkinDrawUtil.DrawRectBorder(spriteBatch, context.PanelRect, edge, 2);
            _state.DrawForeground(spriteBatch, context.Alpha);
        }

        public override void DrawTitleDecoration(SpriteBatch spriteBatch, ChoiceLayoutContext context) {
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 starPos = new(context.PanelRect.Right - 18, context.PanelRect.Y + 14);
            float s = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * context.Alpha;
            DrawStar(spriteBatch, starPos, 3.5f, GetEdgeColor(context.Alpha) * s);
        }

        public override void DrawOptionBackground(SpriteBatch spriteBatch, ChoiceLayoutContext context, ChoiceOptionPresentation option, Rectangle rect, int optionIndex, float hover) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color choiceBg = option.Enabled
                ? Color.Lerp(new Color(8, 20, 34) * 0.35f, new Color(20, 50, 72) * 0.55f, hover)
                : new Color(10, 14, 22) * 0.15f;
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), choiceBg * context.Alpha);
            if (option.Enabled && hover > 0.01f) {
                SkinDrawUtil.DrawRectBorder(spriteBatch, rect, GetEdgeColor(context.Alpha) * (hover * 0.6f), 1);
            }
        }

        private Color GetEdgeColor(float alpha) {
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            return new Color(70, 150, 210) * (alpha * wave);
        }

        private static void DrawStar(SpriteBatch spriteBatch, Vector2 center, float size, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color * 0.85f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
        }
    }
}
