using InnoVault.Narrative.Styling;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.UI;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Base
{
    internal class StoryPopupSkin : PopupSkin
    {
        private Item _iconItem;

        protected virtual Color Fill => new(16, 24, 36);
        protected virtual Color Edge => new(80, 150, 210);

        public override Color HintColor => Edge;

        protected override string ResolveClaimHint() => DialogueSystem.ClaimHint.Value;

        protected override string ResolveContinueHint() => DialogueSystem.PopupContinueHint.Value;

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, float alpha)
            => NarrativeSkinDraw.DrawPanel(spriteBatch, panel, Fill, Edge, alpha);

        public override void DrawIcon(SpriteBatch spriteBatch, PopupLayoutContext context) {
            if (context.IconItemType <= 0) {
                return;
            }

            float appear = MathHelper.Clamp(context.ContentAppear, 0f, 1f);
            float ease = MathF.Sin(appear * MathHelper.PiOver2);
            float iconScaleEase = MathHelper.Lerp(0.35f, 1f, ease);
            float iconAlpha = appear * context.Alpha;
            float bounce = MathF.Sin(MathHelper.Clamp(ease * 1.2f, 0f, 1f) * MathHelper.Pi) * 0.08f;
            float floatOffset = MathF.Sin(context.GlobalTimer * 3.2f + appear) * 4f * appear;
            Vector2 center = context.IconRect.Center.ToVector2() + new Vector2(0f, -floatOffset);

            if (_iconItem == null || _iconItem.type != context.IconItemType) {
                _iconItem = new Item(context.IconItemType);
            }
            ItemSlot.DrawItemIcon(_iconItem, ItemSlot.Context.InWorld, spriteBatch, center,
                1.4f * (iconScaleEase + bounce), 48f, Color.White * iconAlpha);
        }
    }
}
