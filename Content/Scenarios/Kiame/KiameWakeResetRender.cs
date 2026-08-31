using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    /// <summary>
    /// 死亡重启的全屏合成（被害端本机，由 <see cref="KiameWake"/> 时间轴驱动）：
    /// 快门帧把主屏存作照片层（自持 ScreenTargets[0]），之后每帧拷屏 ping-pong，
    /// KikasaReset.fx 里照片化（银盐调+颗粒+晕影）、雨痕冲刷自上而下刷掉照片、
    /// 倒带段实时画面加冷调与回卷抖动；快门与落定的白闪走 CPU 叠层。
    /// 管线镜像 KikasaResetRender，落定白闪保持峰值直到出雨帧、盖住世界切换
    /// </summary>
    internal sealed class KiameWakeResetRender : RenderHandle
    {
        /// <summary>压过子世界水体等后处理；与 KikasaResetRender(2.06) 错开一位</summary>
        public override float Weight => 2.07f;

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
            if (!KiameWake.ShowActive || Main.gameMenu) {
                snapshotPending = false;
                snapshotValid = false;
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）走纯色低质量回退
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }
            Effect fx = EffectLoader.KikasaReset?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch);
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
                DrawLowQualityFallback(spriteBatch);
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
            SetParams(fx);
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

            DrawFlashOverlay(spriteBatch);
            RestoreTargets(graphicsDevice, previousTargets);
        }

        private static void SetParams(Effect fx) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uWash"]?.SetValue(WashProgress());
            fx.Parameters["uRewind"]?.SetValue(RewindGlow());
            fx.Parameters["uSeed"]?.SetValue(KiameWake.ShowSeed);
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
        }

        /// <summary>冲刷进度 0~1：0=照片全覆盖，1=刷尽</summary>
        private static float WashProgress() {
            int timer = KiameWake.ShowTimer;
            if (timer <= KiameWake.ShutterEnd) {
                return 0f;
            }
            return MathHelper.Clamp((timer - KiameWake.ShutterEnd)
                / (float)(KiameWake.WashEnd - KiameWake.ShutterEnd), 0f, 1f);
        }

        /// <summary>倒带冷调 0~1：倒带段快速升起，结算后维持在白闪之下</summary>
        private static float RewindGlow() {
            int timer = KiameWake.ShowTimer;
            if (timer <= KiameWake.WashEnd) {
                return 0f;
            }
            return Math.Min((timer - KiameWake.WashEnd) / 12f, 1f);
        }

        /// <summary>快门与落定的白闪：辉光罩 + 峰值处近全白；
        /// 落定闪升到峰值后保持，出雨帧在纯白里切世界</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch) {
            float flash = FlashStrength();
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

        private static float FlashStrength() {
            int timer = KiameWake.ShowTimer;
            //快门闪：首帧全白快速退
            if (timer <= 8) {
                return 1f - timer / 8f;
            }
            //落定闪：结算帧上冲 3 帧到峰值并保持，主世界余辉接手退场
            if (timer > KiameWake.RewindEnd) {
                return Math.Min((timer - KiameWake.RewindEnd) / 3f, 1f);
            }
            return 0f;
        }

        /// <summary>RT 不可用的纯色回退：照片段压灰 + 倒带段冷罩 + 白闪，结算不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float photoDim = (1f - WashProgress()) * 0.35f;
            float rewind = RewindGlow() * 0.18f;
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

            DrawFlashOverlay(spriteBatch);
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

    /// <summary>
    /// 死亡重启的背景沙漏（简化版）：只画 KikasaHourglass.fx 着色器本体
    /// （成形/升沙/脉冲/溃散），不搬 KikasaResetHourglass 的 CPU 雨丝粒子系统。
    /// 物块之后、NPC/玩家之前，读作背景结构；色板沿用 KikasaInk，
    /// 与鬼伞重启同一座沙漏，玩家一眼认出「这是重启」
    /// </summary>
    internal sealed class KiameWakeHourglassRender : RenderHandle
    {
        public override float Weight => 1.22f;

        /// <summary>画布纵横比，与 KikasaHourglass.fx 常量同源</summary>
        private const float CanvasAspect = 0.80f;

        //玻璃雨丝的流动相位：成形段顺淌而下，倒带段随脉冲向上抽回
        private static float flowPhase;

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || !KiameWake.ShowActive) {
                flowPhase = 0f;
                return;
            }
            int timer = KiameWake.ShowTimer;
            if (timer <= KiameWake.ShutterEnd) {
                return;
            }
            Effect fx = EffectLoader.KikasaHourglass?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || canvas == null || noise == null) {
                return;
            }

            flowPhase += KiameWake.RainRewindActive
                ? 0.012f + 0.030f * KiameWake.RewindPulseRate
                : -0.007f;

            float form = Smooth01((timer - KiameWake.ShutterEnd)
                / (float)(KiameWake.WashEnd - KiameWake.ShutterEnd));
            float disperse = timer > KiameWake.RewindEnd
                ? MathHelper.Clamp((timer - KiameWake.RewindEnd)
                    / (float)(KiameWake.ExitFrame - KiameWake.RewindEnd), 0f, 1f)
                : 0f;
            float alphaIn = MathHelper.Clamp((timer - KiameWake.ShutterEnd) / 10f, 0f, 1f);

            float quadH = Main.screenHeight * 0.62f;
            Vector2 center = CanvasCenter();

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            //共享 uniform 是设备全局状态：每次调用全参数重设，漏一个就串残值
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(KiameWake.ShowSeed);
            fx.Parameters["uForm"]?.SetValue(form);
            fx.Parameters["uFill"]?.SetValue(KiameWake.RewindProgress01);
            fx.Parameters["uPulse"]?.SetValue(KiameWake.RewindPulseRate);
            fx.Parameters["uDisperse"]?.SetValue(disperse);
            fx.Parameters["uAlpha"]?.SetValue(0.92f * alphaIn);
            fx.Parameters["uFlow"]?.SetValue(flowPhase);
            fx.Parameters["uAspect"]?.SetValue(CanvasAspect);
            fx.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
            fx.Parameters["uColSheen"]?.SetValue(KikasaInk.WetSheen.ToVector3());
            fx.CurrentTechnique.Passes[0].Apply();

            float quadW = quadH * CanvasAspect;
            Rectangle dest = new((int)(center.X - quadW * 0.5f),
                (int)(center.Y - quadH * 0.5f), (int)quadW, (int)quadH);
            spriteBatch.Draw(canvas, dest, Color.White);
            spriteBatch.End();
        }

        /// <summary>屏幕锚定 + 死亡点微视差（镜像 KikasaResetHourglass.CanvasCenter）</summary>
        private static Vector2 CanvasCenter() {
            Vector2 camCenter = Main.screenPosition
                + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 off = (KiameWake.ShowAnchor - camCenter) * 0.05f;
            off.X = MathHelper.Clamp(off.X, -40f, 40f);
            off.Y = MathHelper.Clamp(off.Y, -40f, 40f);
            return new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.44f) + off;
        }

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
