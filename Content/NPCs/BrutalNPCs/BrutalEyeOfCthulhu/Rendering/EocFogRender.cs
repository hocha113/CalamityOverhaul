using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering
{
    /// <summary>克眼血雾全屏合成：雾团遮蔽+血幕收拢+血闪，screenTarget ping-pong</summary>
    internal class EocFogRender : RenderHandle
    {
        /// <summary>权重 1.07，居于热浪(1.06)与 Prime 屏效(1.08)之间</summary>
        public override float Weight => 1.072f;

        internal const int MaxBlobs = 10;
        private static readonly Vector4[] blobBuffer = new Vector4[MaxBlobs];
        //上帧收到的雾团数：时停中雾团 AI 停摆（戳过期）靠"上帧有货"闩锁继续扫，遮蔽不塌
        private static int lastBlobCount;

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            //谎言残影与屏效状态每帧推进，与本体是否在屏内无关
            EocRenderHelper.UpdateGhosts();
            EocScreenFX.Update();

            int blobCount = 0;
            if (lastBlobCount > 0 || EocFogCloud.PresenceStamp.ActiveWithin()) {
                blobCount = GatherFogBlobs();
            }
            lastBlobCount = blobCount;

            if (blobCount == 0 && !EocScreenFX.HasAny) {
                return;
            }
            if (screenSwap == null || Main.screenTarget == null) {
                return;
            }
            Effect shader = EffectLoader.EocBloodFog?.Value;
            if (shader == null) {
                return;
            }

            for (int i = blobCount; i < MaxBlobs; i++) {
                blobBuffer[i] = Vector4.Zero;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.017f);
            shader.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            shader.Parameters["blobData"]?.SetValue(blobBuffer);
            shader.Parameters["blobCount"]?.SetValue((float)blobCount);
            shader.Parameters["uVignette"]?.SetValue(EocScreenFX.VignetteIntensity);
            shader.Parameters["uPulse"]?.SetValue(EocScreenFX.PulseIntensity);
            float flash = EocScreenFX.FlashActive
                ? EocScreenFX.FlashIntensity * (1f - EocScreenFX.FlashProgress) : 0f;
            shader.Parameters["uFlash"]?.SetValue(flash);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

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

        /// <summary>收集屏幕附近雾团，就近取前 MaxBlobs 个</summary>
        private static int GatherFogBlobs() {
            int count = 0;
            int fogType = ModContent.ProjectileType<EocFogCloud>();
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != fogType) {
                    continue;
                }
                if (count >= MaxBlobs) {
                    break;
                }
                if (proj.ModProjectile is not EocFogCloud cloud) {
                    continue;
                }
                float density = cloud.CurrentDensity;
                if (density <= 0.02f) {
                    continue;
                }
                //屏外过远剔除
                if (Vector2.DistanceSquared(proj.Center, screenCenter) > 2200f * 2200f) {
                    continue;
                }
                Vector2 uv = WorldToScreenUV(proj.Center);
                blobBuffer[count] = new Vector4(uv.X, uv.Y,
                    PixelsToHeightNorm(cloud.CurrentRadius), density);
                count++;
            }
            return count;
        }

        /// <summary>世界→归一化 uv(含 Zoom)</summary>
        internal static Vector2 WorldToScreenUV(Vector2 worldPos) {
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
        internal static float PixelsToHeightNorm(float pixels) {
            float zoomY = Main.GameViewMatrix.Zoom.Y;
            if (zoomY <= 0f) {
                zoomY = 1f;
            }
            return pixels * zoomY / Main.screenHeight;
        }
    }
}
