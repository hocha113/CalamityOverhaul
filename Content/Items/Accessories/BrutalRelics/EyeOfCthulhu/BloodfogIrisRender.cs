using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Rendering;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳全屏合成：复用克眼 EocBloodFog 着色器，把玩家的雾裹锚点合成为体积血雾。<br/>
    /// 与克眼本体的 EocFogRender(1.072) 各管各的锚点，链式拷屏互不干扰
    /// </summary>
    internal sealed class BloodfogIrisRender : RenderHandle
    {
        /// <summary>认领槽位 1.72</summary>
        public override float Weight => 1.72f;

        private const int MaxBlobs = 10;
        private static readonly Vector4[] blobBuffer = new Vector4[MaxBlobs];

        public override void EndCaptureDraw(SpriteBatch sb, GraphicsDevice gd, RenderTarget2D screenSwap) {
            //屏效状态每帧推进，与是否有雾团无关
            BloodfogScreenFX.Update();

            if (Main.gameMenu) {
                return;
            }
            int blobCount = GatherVeilBlobs();
            if (blobCount == 0 && !BloodfogScreenFX.HasAny) {
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
            shader.Parameters["uVignette"]?.SetValue(BloodfogScreenFX.Vignette);
            shader.Parameters["uPulse"]?.SetValue(BloodfogScreenFX.Pulse);
            shader.Parameters["uFlash"]?.SetValue(BloodfogScreenFX.Flash);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成拷屏贴图
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

        /// <summary>
        /// 收集雾裹锚点：每个雾裹给主团+两颗绕转子团，破开"单团圆气球"的贴纸感；
        /// 复用 EocFogRender 的世界→uv 折算
        /// </summary>
        private static int GatherVeilBlobs() {
            int count = 0;
            int veilType = ModContent.ProjectileType<BloodfogVeilProj>();
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float time = (float)Main.timeForVisualEffects * 0.03f;

            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (count > MaxBlobs - 3) {
                    break;
                }
                if (proj.type != veilType || proj.ModProjectile is not BloodfogVeilProj veil) {
                    continue;
                }
                float density = veil.CurrentDensity;
                if (density <= 0.02f) {
                    continue;
                }
                if (Vector2.DistanceSquared(proj.Center, screenCenter) > 2200f * 2200f) {
                    continue;
                }

                float radius = veil.CurrentRadius;
                //主团
                Vector2 uv = EocFogRender.WorldToScreenUV(proj.Center);
                blobBuffer[count++] = new Vector4(uv.X, uv.Y,
                    EocFogRender.PixelsToHeightNorm(radius), density);
                //两颗子团异相绕转，轮廓随时间呼吸
                for (int k = 0; k < 2; k++) {
                    float ang = time * (k == 0 ? 1f : -0.7f) + proj.whoAmI * 2.3f + k * MathHelper.Pi;
                    Vector2 subPos = proj.Center + ang.ToRotationVector2() * radius * 0.55f;
                    Vector2 subUv = EocFogRender.WorldToScreenUV(subPos);
                    blobBuffer[count++] = new Vector4(subUv.X, subUv.Y,
                        EocFogRender.PixelsToHeightNorm(radius * 0.55f), density * 0.55f);
                }
            }
            return count;
        }
    }

    /// <summary>
    /// 血雾之瞳屏效状态：客户端本地视觉积累器(与 EocScreenFX 同范式)，
    /// 血闪由事件 Push，血幕/心跳由本机玩家雾态每帧推导
    /// </summary>
    internal static class BloodfogScreenFX
    {
        /// <summary>血闪 0~1，免死/伏击命中一次性脉冲</summary>
        internal static float Flash { get; private set; }
        /// <summary>血幕收拢 0~1，本机雾态时缓升</summary>
        internal static float Vignette { get; private set; }
        /// <summary>心跳脉动 0~1</summary>
        internal static float Pulse { get; private set; }

        internal static bool HasAny => Flash > 0.02f || Vignette > 0.02f || Pulse > 0.02f;

        internal static void PushFlash(float intensity) {
            if (VaultUtils.isServer) {
                return;
            }
            Flash = MathHelper.Clamp(Math.Max(Flash, intensity), 0f, 1f);
        }

        /// <summary>渲染句柄每帧驱动：血闪指数退潮，血幕/心跳跟随本机雾态</summary>
        internal static void Update() {
            Flash *= 0.82f;
            if (Flash < 0.02f) {
                Flash = 0f;
            }

            bool localVeiled = !Main.gameMenu && Main.LocalPlayer != null && Main.LocalPlayer.active
                && Main.LocalPlayer.TryGetModPlayer(out BloodfogIrisPlayer mp) && mp.VeilVisualTimer > 0;
            float vignetteGoal = localVeiled ? 0.2f : 0f;
            float pulseGoal = localVeiled ? 0.32f : 0f;
            Vignette = MathHelper.Lerp(Vignette, vignetteGoal, 0.08f);
            Pulse = MathHelper.Lerp(Pulse, pulseGoal, 0.1f);
            if (Vignette < 0.015f && vignetteGoal <= 0f) {
                Vignette = 0f;
            }
            if (Pulse < 0.015f && pulseGoal <= 0f) {
                Pulse = 0f;
            }
        }

        internal static void Clear() {
            Flash = 0f;
            Vignette = 0f;
            Pulse = 0f;
        }
    }
}
