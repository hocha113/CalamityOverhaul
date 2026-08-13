using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering
{
    /// <summary>骷髅王诅咒之幕全屏后效：黑暗领域+冲击环+骨白冲击帧，screenTarget ping-pong</summary>
    internal class SkeletronScreenRender : RenderHandle
    {
        /// <summary>权重 1.092，紧邻机械骷髅王(1.08)之后、本折冷焰批(1.0925)之前</summary>
        public override float Weight => 1.092f;

        private static readonly Vector4[] ringBuffer = new Vector4[SkeletronScreenEffects.MaxRings];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            SkeletronScreenEffects.Update();

            if (!SkeletronScreenEffects.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.SkeletronCurseVeil?.Value;
            if (shader == null || CWRAsset.PerlinNoise?.Value == null) {
                return;
            }

            int count = 0;
            for (int i = 0; i < SkeletronScreenEffects.MaxRings; i++) {
                ref readonly var ring = ref SkeletronScreenEffects.Rings[i];
                if (!ring.Active) {
                    ringBuffer[i] = Vector4.Zero;
                    continue;
                }
                float t = ring.Age / (float)ring.Life;
                float radiusPx = ring.MaxRadiusPx * VaultUtils.EaseOutCubic(t);
                float strength = ring.Intensity * (1f - t) * (1f - t);
                Vector2 uv = WorldToScreenUV(ring.WorldCenter);
                ringBuffer[i] = new Vector4(uv.X, uv.Y, PixelsToHeightNorm(radiusPx), strength);
                count++;
            }

            float flashProgress = SkeletronScreenEffects.FlashLife > 0
                ? MathHelper.Clamp(SkeletronScreenEffects.FlashAge / (float)SkeletronScreenEffects.FlashLife, 0f, 1f)
                : 1f;
            float flash = SkeletronScreenEffects.FlashActive ? SkeletronScreenEffects.FlashIntensity : 0f;

            //视界压缩中心 = 本地玩家
            Vector2 center = WorldToScreenUV(Main.LocalPlayer.Center);

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["uDomain"]?.SetValue(SkeletronScreenEffects.DomainIntensity);
            shader.Parameters["uCenter"]?.SetValue(center);
            shader.Parameters["uFlash"]?.SetValue(flash);
            shader.Parameters["uFlashProgress"]?.SetValue(flashProgress);
            shader.Parameters["ringData"]?.SetValue(ringBuffer);
            shader.Parameters["uNoiseTex"]?.SetValue(CWRAsset.PerlinNoise.Value);

            //拷屏再回写
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

        /// <summary>世界→归一化uv（含Zoom）</summary>
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
