using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Rendering
{
    /// <summary>暴风雪视界全屏后效，screenTarget ping-pong 单pass</summary>
    internal class DeerclopsVeilRender : RenderHandle
    {
        /// <summary>权重 1.07，热浪(1.06)与Prime屏效(1.08)之间</summary>
        public override float Weight => 1.07f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            DeerclopsVeilFX.Update();

            if (!DeerclopsVeilFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            if (EffectLoader.DeerBlizzardVeil?.IsLoaded != true) {
                return;
            }
            Effect shader = EffectLoader.DeerBlizzardVeil.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                return;
            }

            Vector2 bossUV = WorldToScreenUV(DeerclopsVeilFX.BossWorldCenter);
            //boss无效时清晰圈推到屏外(白澈全遮)
            if (!DeerclopsVeilFX.BossValid) {
                bossUV = new Vector2(0.5f, -8f);
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uIntensity"]?.SetValue(DeerclopsVeilFX.Veil);
            shader.Parameters["uWhiteout"]?.SetValue(DeerclopsVeilFX.Whiteout);
            shader.Parameters["uGazeWarn"]?.SetValue(DeerclopsVeilFX.GazeWarn);
            shader.Parameters["uPunish"]?.SetValue(DeerclopsVeilFX.PunishFlash);
            shader.Parameters["uBossUV"]?.SetValue(bossUV);
            shader.Parameters["uClearRadius"]?.SetValue(PixelsToHeightNorm(430f));
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uNoise"]?.SetValue(noise);

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

        /// <summary>世界→归一化uv(含Zoom)</summary>
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
