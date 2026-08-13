using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Rendering
{
    /// <summary>石巨人全屏后效：太阳白闪走自家 FlashTech，冲击环/冲击帧复用 Prime 通用着色器</summary>
    internal class GolemScreenEffectRender : RenderHandle
    {
        /// <summary>权重 1.09，紧邻 Prime 后效(1.08)</summary>
        public override float Weight => 1.094f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            GolemScreenEffects.Update();

            if (!GolemScreenEffects.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }

            if (AnyRingActive() && EffectLoader.PrimeShockRing?.IsLoaded == true) {
                ApplyShockRings(sb, gd, screenSwap);
            }

            if (GolemScreenEffects.FlashActive && EffectLoader.GolemSolarFlare?.IsLoaded == true) {
                ApplySunFlash(sb, gd, screenSwap);
            }

            if (GolemScreenEffects.ImpactActive && EffectLoader.PrimeImpactFrame?.IsLoaded == true) {
                ApplyImpactFrame(sb, gd, screenSwap);
            }
        }

        private static bool AnyRingActive() {
            for (int i = 0; i < GolemScreenEffects.MaxRings; i++) {
                if (GolemScreenEffects.Rings[i].Active) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>拷屏再 shader 回写</summary>
        private static void PingPong(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap, Effect shader) {
            gd.SetRenderTarget(screenSwap);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            sb.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            sb.End();

            gd.SetRenderTarget(Main.screenTarget);
            gd.Clear(Color.Transparent);
            sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(screenSwap, Vector2.Zero, Color.White);
            sb.End();
        }

        private static readonly Vector4[] ringBuffer = new Vector4[GolemScreenEffects.MaxRings];

        private static void ApplyShockRings(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            int count = 0;
            for (int i = 0; i < GolemScreenEffects.MaxRings; i++) {
                ref readonly var ring = ref GolemScreenEffects.Rings[i];
                if (!ring.Active) {
                    continue;
                }

                float t = ring.Age / (float)ring.Life;
                float radiusPx = ring.MaxRadiusPx * VaultUtils.EaseOutCubic(t);
                float strength = ring.Intensity * (1f - t) * (1f - t);
                Vector2 centerUV = WorldToScreenUV(ring.WorldCenter);

                ringBuffer[count] = new Vector4(centerUV.X, centerUV.Y, PixelsToHeightNorm(radiusPx), strength);
                count++;
            }

            if (count == 0) {
                return;
            }
            for (int i = count; i < GolemScreenEffects.MaxRings; i++) {
                ringBuffer[i] = Vector4.Zero;
            }

            Effect shader = EffectLoader.PrimeShockRing.Value;
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["ringData"]?.SetValue(ringBuffer);
            shader.Parameters["ringCount"]?.SetValue((float)count);

            PingPong(sb, gd, screenSwap, shader);
        }

        private static void ApplySunFlash(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            Effect shader = EffectLoader.GolemSolarFlare.Value;
            float progress = MathHelper.Clamp(
                GolemScreenEffects.FlashAge / (float)GolemScreenEffects.FlashLife, 0f, 1f);

            shader.CurrentTechnique = shader.Techniques["FlashTech"];
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uIntensity"]?.SetValue(GolemScreenEffects.FlashIntensity);
            shader.Parameters["uCenter"]?.SetValue(WorldToScreenUV(GolemScreenEffects.FlashWorldCenter));
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

            PingPong(sb, gd, screenSwap, shader);
        }

        private static void ApplyImpactFrame(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            Effect shader = EffectLoader.PrimeImpactFrame.Value;
            shader.Parameters["uIntensity"]?.SetValue(GolemScreenEffects.ImpactIntensity);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(
                GolemScreenEffects.ImpactAge / (float)GolemScreenEffects.ImpactLife, 0f, 1f));

            PingPong(sb, gd, screenSwap, shader);
        }

        /// <summary>世界→归一化 uv(含 Zoom)</summary>
        private static Vector2 WorldToScreenUV(Vector2 worldPos) {
            float screenW = Main.screenWidth;
            float screenH = Main.screenHeight;
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            if (zoom.X <= 0f) {
                zoom.X = 1f;
            }
            if (zoom.Y <= 0f) {
                zoom.Y = 1f;
            }
            Vector2 screenCenterPx = new(screenW * 0.5f, screenH * 0.5f);
            Vector2 viewWorldCenter = Main.screenPosition + screenCenterPx;
            Vector2 screenPx = screenCenterPx + (worldPos - viewWorldCenter) * zoom;
            return new Vector2(screenPx.X / screenW, screenPx.Y / screenH);
        }

        /// <summary>像素→屏高归一化</summary>
        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }
}
