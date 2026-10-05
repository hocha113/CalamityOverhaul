using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraFinaleBlurRender : RenderHandle
{
    // InnoVault evaluates this at setup, so it must be stable. The actual draw is
    // skipped unless a burst has an active blur pulse.
    public override bool RequireScreenCapture => true;
    public override bool Active => !Main.dedServ && ModContent.GetInstance<SakuraFinaleFeedback>().HasBlur;

    public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) =>
        ModContent.GetInstance<SakuraFinaleFeedback>().DrawBlur(spriteBatch, graphicsDevice);
}
