using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts
{
    /// <summary>
    /// 大范围重启的全屏合成：每帧拷屏 ping-pong，shader 里潮水自屏底吞没实时画面
    /// （泡沫锋/水下深渊调色/焦散/海雪），倒带段回卷抖动与渊眼注视。
    /// 不做照片定格——定格是鬼伞的身份，比目鱼是活世界被海吞没后时间倒流。
    /// 结算白闪走 CPU 叠层。旁观者按观看距离同样合成。
    /// 简约模式（DomainConciseDisplay）与技术性 RT 不可用一律走纯色低质量回退
    /// </summary>
    internal sealed class HalibutResetRender : RenderHandle
    {
        /// <summary>压过领域类后处理，排在鬼伞重启（2.06）之下错开；两场演出全局互斥不会同帧</summary>
        public override float Weight => 2.04f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            HalibutReset.ResetShow show = HalibutReset.Active;
            if (show == null || Main.gameMenu) {
                return;
            }
            if (!HalibutReset.LocallyViewed) {
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）或玩家选了简约领域一律纯色回退
            if (DomainVisuals.Concise || RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch, show);
                return;
            }
            Effect fx = EffectLoader.HalibutRestartTide?.Value;
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

            //拷屏交换期间任何异常都必须把绑定还回主屏，否则后续绘制全落到屏外
            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
            try {
                //1. 拷屏到交换缓冲
                graphicsDevice.SetRenderTarget(screenSwap);
                graphicsDevice.Clear(Color.Transparent);
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                spriteBatch.End();

                //2. 潮汐吞没合成写回主屏：s0=实时屏 s1=噪声
                graphicsDevice.SetRenderTarget(Main.screenTarget);
                graphicsDevice.Clear(Color.Transparent);
                SetParams(fx, show);
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                fx.CurrentTechnique = fx.Techniques["TechTide"];
                fx.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
                spriteBatch.End();

                DrawFlashOverlay(spriteBatch, show);
            }
            finally {
                RestoreTargets(graphicsDevice, previousTargets);
            }
        }

        /// <summary>帧号包络：from→to 平滑升到 1（smoothstep），两端钳制</summary>
        private static float Env(float from, float to, float timer) {
            float x = MathHelper.Clamp((timer - from) / (to - from), 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static void SetParams(Effect fx, HalibutReset.ResetShow show) {
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uFlood"]?.SetValue(FloodProgress(show));
            fx.Parameters["uRewind"]?.SetValue(RewindGrade(show));
            fx.Parameters["uPulse"]?.SetValue(HalibutReset.RewindPulseRate);
            fx.Parameters["uEye"]?.SetValue(EyeOpen(show));
            fx.Parameters["uDim"]?.SetValue(Presence(show));
            fx.Parameters["uSeed"]?.SetValue(show.Seed);
            fx.Parameters["uAspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
            fx.Parameters["uEyeUv"]?.SetValue(EyeUv(show));
        }

        /// <summary>潮水覆盖 0~1：起势后涌起，倒带全程漫顶，结算后退潮</summary>
        internal static float FloodProgress(HalibutReset.ResetShow show) {
            float rise = Env(8f, HalibutReset.FloodEnd - 8f, show.Timer);
            float fall = Env(HalibutReset.RewindEnd + 6f,
                HalibutReset.TotalFrames - 4f, show.Timer);
            return rise * (1f - fall);
        }

        /// <summary>倒带调色 0~1：漫顶后快速升起，结算段退场</summary>
        private static float RewindGrade(HalibutReset.ResetShow show) {
            float head = Env(HalibutReset.FloodEnd,
                HalibutReset.FloodEnd + 14f, show.Timer);
            float tail = Env(HalibutReset.RewindEnd,
                HalibutReset.RewindEnd + 12f, show.Timer);
            return head * (1f - tail);
        }

        /// <summary>渊眼开度：倒带中段缓睁，结算帧猛地阖上——重启的那一眨</summary>
        private static float EyeOpen(HalibutReset.ResetShow show) {
            float open = Env(HalibutReset.FloodEnd + 18f,
                HalibutReset.FloodEnd + 64f, show.Timer);
            float close = Env(HalibutReset.RewindEnd - 8f,
                HalibutReset.RewindEnd + 2f, show.Timer);
            return open * (1f - close);
        }

        /// <summary>演出在场度：干侧风暴压暗的包络</summary>
        private static float Presence(HalibutReset.ResetShow show) {
            float head = Env(0f, 14f, show.Timer);
            float tail = Env(HalibutReset.TotalFrames - 16f,
                HalibutReset.TotalFrames - 2f, show.Timer);
            return head * (1f - tail);
        }

        /// <summary>渊眼锚点：施术者头顶上方一段，钳在屏内舒适区；
        /// 世界→屏幕走 GameViewMatrix 变换（缩放/运镜下裸除 zoom 会算歪）</summary>
        private static Vector2 EyeUv(HalibutReset.ResetShow show) {
            Player owner = Main.player[show.OwnerWho];
            Vector2 uv = new(0.5f, 0.30f);
            if (owner?.active == true) {
                Vector2 screenPx = Vector2.Transform(owner.Center - Main.screenPosition,
                    Main.GameViewMatrix.TransformationMatrix);
                uv = new Vector2(screenPx.X / Main.screenWidth, screenPx.Y / Main.screenHeight - 0.17f);
            }
            uv.X = MathHelper.Clamp(uv.X, 0.28f, 0.72f);
            uv.Y = MathHelper.Clamp(uv.Y, 0.20f, 0.46f);
            return uv;
        }

        /// <summary>
        /// 世界层鱼汛装饰：潮水里的鱼群自作用圈边界向施术者洄游。
        /// 实体层末尾绘制，会被随后的潮汐全屏合成一并调色成水下色
        /// </summary>
        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || HalibutReset.Active == null || !HalibutReset.LocallyViewed) {
                return;
            }
            HalibutReset.DrawGarnish(spriteBatch);
        }

        /// <summary>结算白闪：辉光罩 + 峰值处近全白，冷白偏深海色温</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch, HalibutReset.ResetShow show) {
            float flash = FlashStrength(show);
            if (flash <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            Color soft = new(0.80f, 0.92f, 0.96f, 0f);
            Color hardCol = new(222, 240, 244);

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

        private static float FlashStrength(HalibutReset.ResetShow show) {
            //落定闪：结算帧上冲 3 帧、退 14 帧——空化塌缩的那一瞬
            if (show.Timer > HalibutReset.RewindEnd) {
                int k = show.Timer - HalibutReset.RewindEnd;
                return k <= 3 ? k / 3f : MathF.Max(0f, 1f - (k - 3) / 14f);
            }
            return 0f;
        }

        /// <summary>RT 不可用/简约模式的纯色回退：下半屏水压蓝 + 倒带冷罩 + 白闪，结算不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch,
            HalibutReset.ResetShow show) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float flood = FloodProgress(show);
            float rewind = RewindGrade(show) * 0.20f;
            //水线以下压深蓝：粗略两段近似潮位
            int surface = (int)(Main.screenHeight * (1f - flood * 1.34f));
            surface = Math.Clamp(surface, 0, Main.screenHeight);
            Rectangle underRect = new(0, surface, Main.screenWidth, Main.screenHeight - surface);
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            if (underRect.Height > 0 && flood > 0.002f) {
                spriteBatch.Draw(white, underRect, new Color(16, 44, 84) * 0.42f);
            }
            if (rewind > 0.002f) {
                spriteBatch.Draw(white, full, new Color(20, 40, 60) * rewind);
            }
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch, show);
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
