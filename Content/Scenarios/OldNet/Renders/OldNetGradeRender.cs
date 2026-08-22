using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Renders
{
    /// <summary>
    /// 旧网世界内分级 + 地形数字化提边（OldNetGrade.fx 消费端）。
    /// 管线契约镜像 CyberspaceRender.ApplyFullScreenShader：物块层后
    /// screenTarget→screenSwap 拷屏，shader 重写回 screenTarget；
    /// RT 不可用时直接跳过，氛围由 Filter 轻染与压光兜底，不做 CPU 复刻
    /// </summary>
    internal class OldNetGradeRender : RenderHandle
    {
        public override float Weight => 1.45f;

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || !OldNetWorld.Active) {
                return;
            }
            float presence = OldNetAmbience.Presence;
            if (presence <= 0.01f) {
                return;
            }
            //技术门：Retro/Trippy 光照或低水质会释放 screenTarget
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            Effect shader = EffectLoader.OldNetGrade?.Value;
            if (shader == null) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed
                || Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //世界视口换算（含缩放）：提边 eps 与虚线用世界坐标
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            Vector2 screenPixels = Main.ScreenSize.ToVector2();
            Vector2 worldViewSize = screenPixels / zoom;
            Vector2 worldViewOrigin = Main.screenPosition
                + screenPixels * (Vector2.One - Vector2.One / zoom) * 0.5f;

            //带内腐化与天幕同口径（按玩家所在列）
            float corrupt = OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f));

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects / 60f);
            shader.Parameters["uIntensity"]?.SetValue(presence);
            shader.Parameters["uCorrupt"]?.SetValue(corrupt);
            shader.Parameters["uGlitch"]?.SetValue(OldNetSkyEvents.Glitch);
            shader.Parameters["uScreenSize"]?.SetValue(screenPixels);
            shader.Parameters["screenPosition"]?.SetValue(worldViewOrigin);
            shader.Parameters["worldViewSize"]?.SetValue(worldViewSize);

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }
    }
}
