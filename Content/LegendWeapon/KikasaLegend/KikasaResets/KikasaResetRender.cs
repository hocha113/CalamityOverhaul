using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启的全屏合成：演出首帧把主屏存作照片层（自持 ScreenTargets[0]），
    /// 之后每帧拷屏 ping-pong——shader 里照片化（去饱和银盐调+胶片颗粒+晕影）、
    /// 雨痕冲刷遮罩自上而下刷掉照片、倒带段实时画面加冷调与回卷抖动。
    /// 快门与落定的白闪走 CPU 叠层。旁观者按观看距离同样合成
    /// </summary>
    internal sealed class KikasaResetRender : RenderHandle
    {
        /// <summary>照片定格压过一切鬼伞后处理：领域(1.24)/翻转(2.02)/鬼梦(2.03)之后压轴</summary>
        public override float Weight => 2.06f;

        /// <summary>[0]=定格照片帧</summary>
        public override int ScreenSlot => 1;

        private static bool snapshotPending;
        private static bool snapshotValid;

        /// <summary>演出开始帧请求定格：下一次全屏合成把主屏存进照片层</summary>
        internal static void RequestSnapshot() {
            snapshotPending = true;
            snapshotValid = false;
        }

        /// <summary>分辨率变化重建 RT 后照片内容已丢，余下演出退级纯色</summary>
        public override void OnResolutionChanged(Vector2 screenSize) {
            snapshotPending = false;
            snapshotValid = false;
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            KikasaReset.ResetShow show = KikasaReset.Active;
            if (show == null || Main.gameMenu) {
                snapshotPending = false;
                snapshotValid = false;
                return;
            }
            if (!KikasaReset.LocallyViewed) {
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）走纯色低质量回退
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch, show);
                return;
            }
            Effect fx = EffectLoader.KikasaReset?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawLowQualityFallback(spriteBatch, show);
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch, show);
                return;
            }

            RenderTarget2D photo = GetPhotoTarget();
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //定格捕获：快门帧的完整主屏存作照片
            if (snapshotPending && photo != null) {
                graphicsDevice.SetRenderTarget(photo);
                graphicsDevice.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                spriteBatch.End();
                graphicsDevice.SetRenderTarget(Main.screenTarget);
                snapshotValid = true;
                snapshotPending = false;
            }

            //照片拿不到（分辨率变化/捕获失败）：整场降级为纯色，形态与结算不受影响
            if (!snapshotValid || photo == null) {
                DrawLowQualityFallback(spriteBatch, show);
                RestoreTargets(graphicsDevice, previousTargets);
                return;
            }

            //1. 拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //2. 照片+冲刷+倒带冷调合成写回主屏：s0=实时屏 s1=照片 s2=噪声
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            SetParams(fx, show);
            graphicsDevice.Textures[1] = photo;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearClamp;
            graphicsDevice.Textures[2] = noise;
            graphicsDevice.SamplerStates[2] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechReset"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch, show);
            RestoreTargets(graphicsDevice, previousTargets);
        }

        private static void SetParams(Effect fx, KikasaReset.ResetShow show) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uWash"]?.SetValue(WashProgress(show));
            fx.Parameters["uRewind"]?.SetValue(RewindGlow(show));
            fx.Parameters["uSeed"]?.SetValue(show.Seed);
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
        }

        /// <summary>冲刷进度 0~1：0=照片全覆盖，1=刷尽</summary>
        private static float WashProgress(KikasaReset.ResetShow show) {
            if (show.Timer <= KikasaReset.SnapshotEnd) {
                return 0f;
            }
            return MathHelper.Clamp(
                (show.Timer - KikasaReset.SnapshotEnd)
                / (float)(KikasaReset.WashEnd - KikasaReset.SnapshotEnd), 0f, 1f);
        }

        /// <summary>倒带冷调 0~1：倒带段快速升起，落定段退场</summary>
        private static float RewindGlow(KikasaReset.ResetShow show) {
            if (show.Timer <= KikasaReset.WashEnd) {
                return 0f;
            }
            if (show.Timer <= KikasaReset.RewindEnd) {
                return Math.Min((show.Timer - KikasaReset.WashEnd) / 12f, 1f);
            }
            return MathHelper.Clamp(1f - (show.Timer - KikasaReset.RewindEnd)
                / (float)(KikasaReset.TotalFrames - KikasaReset.RewindEnd), 0f, 1f);
        }

        /// <summary>快门与落定的白闪：辉光罩 + 峰值处近全白，冷白随鬼雨色温</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch, KikasaReset.ResetShow show) {
            float flash = FlashStrength(show);
            if (flash <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            Color soft = new(0.82f, 0.90f, 0.92f, 0f);
            Color hardCol = new(226, 236, 238);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(white, full, soft * (0.55f * flash));
            spriteBatch.End();

            if (flash > 0.55f) {
                float hard = (flash - 0.55f) / 0.45f;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                spriteBatch.Draw(white, full, hardCol * (0.92f * hard));
                spriteBatch.End();
            }
        }

        private static float FlashStrength(KikasaReset.ResetShow show) {
            //快门闪：首帧全白快速退
            if (show.Timer <= 8) {
                return 1f - show.Timer / 8f;
            }
            //落定闪：结算帧上冲 3 帧、退 12 帧
            if (show.Timer > KikasaReset.RewindEnd) {
                int k = show.Timer - KikasaReset.RewindEnd;
                return k <= 3 ? k / 3f : MathF.Max(0f, 1f - (k - 3) / 12f);
            }
            return 0f;
        }

        /// <summary>RT 不可用的纯色回退：照片段压灰 + 倒带段冷罩 + 白闪，结算不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch,
            KikasaReset.ResetShow show) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float photoDim = (1f - WashProgress(show)) * 0.35f;
            float rewind = RewindGlow(show) * 0.18f;
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            if (photoDim > 0.002f) {
                spriteBatch.Draw(white, full, new Color(118, 126, 130) * photoDim);
            }
            if (rewind > 0.002f) {
                spriteBatch.Draw(white, full, new Color(24, 40, 52) * rewind);
            }
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch, show);
        }

        private RenderTarget2D GetPhotoTarget() {
            if (ScreenTargets == null || ScreenTargets.Length < 1) {
                return null;
            }
            RenderTarget2D photo = ScreenTargets[0];
            if (photo == null || photo.IsDisposed) {
                return null;
            }
            //分辨率变化后尺寸不符则放弃照片层，走纯色回退
            if (photo.Width != Main.screenTarget.Width
                || photo.Height != Main.screenTarget.Height) {
                return null;
            }
            return photo;
        }

        private static void RestoreTargets(GraphicsDevice graphicsDevice,
            RenderTargetBinding[] previousTargets) {
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }
    }
}
