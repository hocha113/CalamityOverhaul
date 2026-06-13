using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Renders
{
    /// <summary>
    /// 机械骷髅王全屏后处理——热浪扭曲、冲击环、终爆冲击帧，screenTarget/screenSwap ping-pong 回写
    /// <br/>状态 <see cref="PrimeScreenEffects"/> 持有，本类每帧衰减驱动；无命中处透传防黑屏
    /// </summary>
    internal class PrimeScreenEffectRender : RenderHandle
    {
        /// <summary>权重 1.08，在热浪(1.06)与扭曲(1.2)之间</summary>
        public override float Weight => 1.08f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            PrimeScreenEffects.Update();

            if (!PrimeScreenEffects.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }

            if (PrimeScreenEffects.HeatIntensity > 0.03f
                && EffectLoader.PrimeHeatWake?.IsLoaded == true) {
                ApplyHeatWake(sb, gd, screenSwap);
            }

            if (AnyRingActive() && EffectLoader.PrimeShockRing?.IsLoaded == true) {
                ApplyShockRings(sb, gd, screenSwap);
            }

            if (PrimeScreenEffects.ImpactActive
                && EffectLoader.PrimeImpactFrame?.IsLoaded == true) {
                ApplyImpactFrame(sb, gd, screenSwap);
            }
        }

        private static bool AnyRingActive() {
            for (int i = 0; i < PrimeScreenEffects.MaxRings; i++) {
                if (PrimeScreenEffects.Rings[i].Active) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>拷屏到 screenSwap 再带着 shader 写回 screenTarget</summary>
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

        private static void ApplyHeatWake(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            Effect shader = EffectLoader.PrimeHeatWake.Value;
            Vector2 centerUV = WorldToScreenUV(PrimeScreenEffects.HeatWorldCenter);

            //源离屏过远时跳过全屏 pass
            if (centerUV.X < -0.5f || centerUV.X > 1.5f || centerUV.Y < -0.5f || centerUV.Y > 1.5f) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uIntensity"]?.SetValue(PrimeScreenEffects.HeatIntensity);
            shader.Parameters["uCenter"]?.SetValue(centerUV);
            shader.Parameters["uDir"]?.SetValue(PrimeScreenEffects.HeatDirection);
            shader.Parameters["uRadius"]?.SetValue(PixelsToHeightNorm(340f));
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uNoise"]?.SetValue(CWRAsset.PerlinNoise.Value);

            PingPong(sb, gd, screenSwap, shader);
        }

        private static readonly Vector4[] ringBuffer = new Vector4[PrimeScreenEffects.MaxRings];

        private static void ApplyShockRings(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            int count = 0;
            for (int i = 0; i < PrimeScreenEffects.MaxRings; i++) {
                ref readonly var ring = ref PrimeScreenEffects.Rings[i];
                if (!ring.Active) {
                    continue;
                }

                float t = ring.Age / (float)ring.Life;
                //环半径 easeOut 扩张，强度随寿命衰减
                float radiusPx = ring.MaxRadiusPx * CWRUtils.EaseOutCubic(t);
                float strength = ring.Intensity * (1f - t) * (1f - t);
                Vector2 centerUV = WorldToScreenUV(ring.WorldCenter);

                ringBuffer[count] = new Vector4(centerUV.X, centerUV.Y, PixelsToHeightNorm(radiusPx), strength);
                count++;
            }

            if (count == 0) {
                return;
            }
            for (int i = count; i < PrimeScreenEffects.MaxRings; i++) {
                ringBuffer[i] = Vector4.Zero;
            }

            Effect shader = EffectLoader.PrimeShockRing.Value;
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["ringData"]?.SetValue(ringBuffer);
            shader.Parameters["ringCount"]?.SetValue((float)count);

            PingPong(sb, gd, screenSwap, shader);
        }

        private static void ApplyImpactFrame(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            Effect shader = EffectLoader.PrimeImpactFrame.Value;
            shader.Parameters["uIntensity"]?.SetValue(PrimeScreenEffects.ImpactIntensity);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(
                PrimeScreenEffects.ImpactAge / (float)PrimeScreenEffects.ImpactLife, 0f, 1f));

            PingPong(sb, gd, screenSwap, shader);
        }

        /// <summary>世界坐标 → 归一化 uv（含 GameViewMatrix.Zoom）</summary>
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

        /// <summary>像素长度 → 屏幕高度归一化（与着色器距离场量纲一致）</summary>
        private static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }
}
