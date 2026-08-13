using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>光之女皇全屏棱彩后效：色散脉冲+昼形态环境描边，screenTarget ping-pong</summary>
    internal class EmpressScreenRender : RenderHandle
    {
        /// <summary>权重 1.088，避开 Prime(1.08) 与并行Boss扎堆的 1.09</summary>
        public override float Weight => 1.088f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            EmpressScreenFX.Update();

            if (!EmpressScreenFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.EmpressScreenPrism?.Value;
            if (shader == null) {
                return;
            }

            float pulseP = EmpressScreenFX.PulseActive
                ? MathHelper.Clamp(EmpressScreenFX.PulseAge / (float)EmpressScreenFX.PulseLife, 0f, 1f)
                : 1f;
            float pulseI = EmpressScreenFX.PulseActive ? EmpressScreenFX.PulseIntensity : 0f;

            Vector2 centerUV = WorldToScreenUV(EmpressScreenFX.PulseWorldCenter);
            //脉冲中心离屏过远则只保留环境档
            if (centerUV.X < -0.6f || centerUV.X > 1.6f || centerUV.Y < -0.6f || centerUV.Y > 1.6f) {
                pulseI = 0f;
            }

            if (pulseI <= 0.01f && EmpressScreenFX.AmbientGrade <= 0.012f) {
                return;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uProgress"]?.SetValue(pulseP);
            shader.Parameters["uIntensity"]?.SetValue(pulseI);
            shader.Parameters["uAmbient"]?.SetValue(EmpressScreenFX.AmbientGrade);
            shader.Parameters["uCenter"]?.SetValue(centerUV);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

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
    }
}
