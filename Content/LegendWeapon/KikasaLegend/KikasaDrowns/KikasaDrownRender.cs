using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 沉溺鬼影的 RT 留影入口：借屏交换缓冲开保屏窗口，逐节捕获交给
    /// <see cref="KikasaDrownFX.RunCaptures"/>；守卫与还屏流程同肢解剪影 RT。
    /// RT 生命周期归演出记录所有，谢幕与清场时由 FX 层统一释放
    /// </summary>
    internal sealed class KikasaDrownRender : RenderHandle
    {
        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || !KikasaDrownFX.HasPendingCaptures()) {
                return;
            }

            //低质量光照/RT 异常时放弃捕获，该节自动走裸贴图回退，演出不空窗

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //先保屏、screenTarget 一旦重绑定内容即被丢弃

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            KikasaDrownFX.RunCaptures(spriteBatch, graphicsDevice);

            //还屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原进入时的 RT 绑定，避免改变上层管线对活动 RT 的预期

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }
    }
}
