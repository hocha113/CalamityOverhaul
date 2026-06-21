using CalamityOverhaul.Content.Narrative.Presentation.Skins.Base;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using InnoVault.Narrative.Presentation.Popups;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.SHPC
{
    internal sealed class SHPCPopupSkin : StoryPopupSkin
    {
        private readonly SHPCPanelState _state = new();

        public override Color TitleColor => new(210, 245, 255);
        public override Color BodyColor => new(160, 230, 250);
        public override Color HintColor => SHPCPanelState.NeonBlueColor;

        public override void Update(PopupLayoutContext context) => _state.Update(context.PanelRect, context.Alpha > 0.01f);

        public override void Reset() => _state.Reset();

        public override void DrawPanel(SpriteBatch spriteBatch, PopupLayoutContext context)
            => SHPCPanelDraw.DrawCyberBackground(spriteBatch, context.PanelRect, context.Alpha, _state);

        public override void DrawFrame(SpriteBatch spriteBatch, PopupLayoutContext context) {
            float pulse = MathF.Sin(_state.NeonPulse * 1.2f) * 0.15f + 0.85f;
            SkinDrawUtil.DrawRectBorder(spriteBatch, context.PanelRect, SHPCPanelState.NeonBlueColor * (context.Alpha * 0.7f * pulse), 2);
        }

        public override void DrawParticles(SpriteBatch spriteBatch, PopupLayoutContext context)
            => _state.DrawParticles(spriteBatch, context.Alpha);
    }
}
