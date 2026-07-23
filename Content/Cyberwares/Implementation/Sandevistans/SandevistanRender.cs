using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>EndCapture 全屏后处理，径向模糊/色差/暗角/辉光
    /// <br/>RT 安全门同 <see cref="Content.HackTimes.HackTimeRender"/>、<see cref="Content.LegendWeapon.SHPCLegend.Cyberspaces.CyberspaceRender"/>
    /// 低水波/Retro/RT 异常时跳过，残影+HUD 顶上</summary>
    internal class SandevistanRender : RenderHandle
    {
        /// <summary>权重 1.1，残影 RT 之后</summary>
        public override float Weight => 1.1f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            float effectIntensity = Sandevistan.ScreenEffectIntensity;
            if (effectIntensity <= 0.001f) {
                return;
            }

            Effect shader = SandevistanAssets.SandevistanScreen;
            if (shader == null) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }

            //低水波/低光照或 RT 异常，跳过，防强写顶替 backbuffer
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            //活动 RT 非 screenTarget 则放弃本帧
            if (!RenderQualitySafety.IsScreenTargetActive(gd)) {
                return;
            }

            //进前 RT，结束还原
            RenderTargetBinding[] previousTargets = gd.GetRenderTargets();

            //拷屏到 swap
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            //玩家屏幕 uv
            Vector2 playerScreen = Main.LocalPlayer.Center - Main.screenPosition;
            Vector2 playerUV = new Vector2(
                playerScreen.X / Main.screenWidth,
                playerScreen.Y / Main.screenHeight
            );
            playerUV = Vector2.Clamp(playerUV, Vector2.Zero, Vector2.One);

            //着色器内乘 intensity，勿双重缩放
            shader.Parameters["intensity"]?.SetValue(effectIntensity);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["chromaticOffset"]?.SetValue(0.005f);
            shader.Parameters["vignetteStrength"]?.SetValue(0.4f);
            shader.Parameters["playerCenter"]?.SetValue(playerUV);
            shader.Parameters["radialBlurStrength"]?.SetValue(0.04f);

            //回写主屏
            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();

            //还原进前 RT
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                gd.SetRenderTargets(previousTargets);
            }
        }
    }
}
