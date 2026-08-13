using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>
    /// 天体全屏后效：星光冲击环 + 超新星虹膜 + 引力昏暗，单 pass 门控叠加。
    /// 权重 1.16，先于扭曲(1.2)，让透镜一并弯折环带
    /// </summary>
    internal class MLordScreenRender : RenderHandle
    {
        public override float Weight => 1.16f;

        private static readonly Vector4[] ringBuffer = new Vector4[MLordScreenEffects.MaxRings];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            MLordScreenEffects.Update();

            if (!MLordScreenEffects.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.MLordCelestial?.IsLoaded != true) {
                return;
            }

            Effect shader = EffectLoader.MLordCelestial.Value;

            //环带
            int count = 0;
            for (int i = 0; i < MLordScreenEffects.MaxRings; i++) {
                ref readonly var ring = ref MLordScreenEffects.Rings[i];
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
            for (int i = count; i < MLordScreenEffects.MaxRings; i++) {
                ringBuffer[i] = Vector4.Zero;
            }

            //超新星虹膜
            float novaStrength = 0f;
            float novaProgress = 0f;
            Vector2 novaUV = Vector2.Zero;
            if (MLordScreenEffects.NovaActive) {
                novaProgress = MLordScreenEffects.NovaAge / (float)MLordScreenEffects.NovaLife;
                novaStrength = MLordScreenEffects.NovaIntensity * (1f - novaProgress * novaProgress);
                novaUV = WorldToScreenUV(MLordScreenEffects.NovaWorldCenter);
            }

            //引力昏暗
            float dim = MLordScreenEffects.GravityDim;
            Vector2 dimUV = WorldToScreenUV(MLordScreenEffects.GravityDimCenter);

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["ringData"]?.SetValue(ringBuffer);
            shader.Parameters["ringCount"]?.SetValue((float)count);
            shader.Parameters["uNova"]?.SetValue(new Vector4(novaUV.X, novaUV.Y, novaProgress, novaStrength));
            shader.Parameters["uDim"]?.SetValue(new Vector4(dimUV.X, dimUV.Y, 0f, dim));

            PingPong(sb, gd, screenSwap, shader);
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

        /// <summary>世界→归一化 uv（含 Zoom）</summary>
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
