using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>
    /// 机械骷髅王全屏后处理：冲击波环 / 终爆冲击帧 / 冲刺热浪扭曲。
    /// </summary>
    internal class PrimeScreenEffectRender : RenderHandle
    {
        public override float Weight => 1.08f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            PrimeScreenEffects.Tick();
            if (!PrimeScreenEffects.HasActive) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }

            Effect shader = ResolveShader();
            if (shader == null) {
                return;
            }

            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            ApplyShaderParams(shader);

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        private static Effect ResolveShader() {
            return PrimeScreenEffects.ActiveType switch {
                PrimeScreenEffectType.ShockRing when EffectLoader.PrimeShockRing?.IsLoaded == true
                    => EffectLoader.PrimeShockRing.Value,
                PrimeScreenEffectType.ImpactFrame when EffectLoader.PrimeImpactFrame?.IsLoaded == true
                    => EffectLoader.PrimeImpactFrame.Value,
                PrimeScreenEffectType.HeatWake when EffectLoader.PrimeHeatWake?.IsLoaded == true
                    => EffectLoader.PrimeHeatWake.Value,
                _ => null,
            };
        }

        private static void ApplyShaderParams(Effect shader) {
            float time = (float)Main.timeForVisualEffects * 0.018f;
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenter = screenSize * 0.5f;
            Vector2 viewCenter = Main.screenPosition + screenCenter;
            Vector2 worldOffset = PrimeScreenEffects.WorldCenter - viewCenter;
            Vector2 screenPx = screenCenter + worldOffset * zoom;
            Vector2 normalized = new(screenPx.X / screenSize.X, screenPx.Y / screenSize.Y);
            float radiusNorm = PrimeScreenEffects.Radius * zoom.Y / screenSize.Y;
            float progress = 1f - PrimeScreenEffects.RemainingFrames / 24f;

            switch (PrimeScreenEffects.ActiveType) {
                case PrimeScreenEffectType.ShockRing:
                    shader.Parameters["globalTime"]?.SetValue(time);
                    shader.Parameters["shockwaveIntensity"]?.SetValue(PrimeScreenEffects.Intensity);
                    shader.Parameters["ringRadius"]?.SetValue(MathHelper.Clamp(radiusNorm + progress * 0.35f, 0f, 1.2f));
                    shader.Parameters["ringThickness"]?.SetValue(0.06f);
                    shader.Parameters["squishY"]?.SetValue(0.72f);
                    shader.Parameters["uNoise"]?.SetValue(CWRAsset.Extra_193.Value);
                    break;
                case PrimeScreenEffectType.ImpactFrame:
                    shader.Parameters["uIntensity"]?.SetValue(PrimeScreenEffects.Intensity);
                    shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
                    break;
                case PrimeScreenEffectType.HeatWake:
                    shader.Parameters["uTime"]?.SetValue(time);
                    shader.Parameters["uIntensity"]?.SetValue(PrimeScreenEffects.Intensity);
                    shader.Parameters["uProgress"]?.SetValue(PrimeScreenEffects.Progress);
                    shader.Parameters["uRotation"]?.SetValue(normalized.X * 6.28f);
                    break;
            }
        }
    }
}
