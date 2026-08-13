using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>克脑心跳全屏后效，screenTarget ping-pong</summary>
    internal class BrainScreenRender : RenderHandle
    {
        /// <summary>权重 1.075，在热浪(1.06)与机械骷髅王(1.08)之间</summary>
        public override float Weight => 1.075f;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            BrainHeartbeat.Update();

            if (!BrainHeartbeat.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.BrainHeartbeatPulse?.Value;
            if (shader == null) {
                return;
            }

            Vector2 centerUV = WorldToScreenUV(BrainHeartbeat.WorldCenter);
            //离屏过远时钳到屏缘，保住方向感（黑幕/血幕仍须生效）
            centerUV = Vector2.Clamp(centerUV, new Vector2(-0.4f), new Vector2(1.4f));

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.02f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uCenter"]?.SetValue(centerUV);
            shader.Parameters["uPulse"]?.SetValue(BrainHeartbeat.Pulse);
            shader.Parameters["uIntensity"]?.SetValue(BrainHeartbeat.Intensity);
            shader.Parameters["uVeil"]?.SetValue(BrainHeartbeat.Veil);
            shader.Parameters["uBlackout"]?.SetValue(BrainHeartbeat.Blackout);
            shader.Parameters["uFlash"]?.SetValue(BrainHeartbeat.ImpactFlash);

            //拷屏再 shader 回写
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
    }
}
