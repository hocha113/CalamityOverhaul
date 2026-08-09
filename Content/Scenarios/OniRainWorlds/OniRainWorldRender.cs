using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 入雨演出的全屏合成：拷屏→镜像着色（水位线以下是真正的垂直镜像倒影，
    /// 水位线从屏底涨到缝线后锁定）→绕屏幕中心旋转写回。<br/>
    /// 翻转期镜像 x 随 rollProgress 收敛为点反射，θ=π 时 180°翻转∘点反射=恒等、
    /// 与真实渲染零跳变交接；收敛中段的横向坍缩被峰值角速度+拖影+结算白闪遮蔽。<br/>
    /// 结算后 uSwallow 把水面向上吞满全屏，θ=π 时输出即输入，直接停用。
    /// </summary>
    internal sealed class OniRainWorldRender : RenderHandle
    {
        /// <summary>权重 2.0，翻转必须是最后一道后处理</summary>
        public override float Weight => 2.0f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || !OniRainWorldTransition.RenderActive) {
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）走纯色低质量回退
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }

            Effect fx = EffectLoader.OniRainWorld?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed
                || Main.screenTargetSwap == null || Main.screenTargetSwap.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //1. 拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //2. 镜像合成到第二交换屏
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            SetMirrorParams(fx);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechMirror"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //3. 绕屏幕中心旋转写回主屏，覆盖缩放保证旋转中途不露角
            float theta = OniRainWorldTransition.RollAngle;
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(new Color(10, 14, 17));
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, BuildRollMatrix(theta));
            spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawRotationSmear(spriteBatch, theta);
            DrawFlashOverlay(spriteBatch);

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        private static void SetMirrorParams(Effect fx) {
            float w = Main.screenWidth;
            float h = Main.screenHeight;
            float rollProgress = MathHelper.Clamp(
                OniRainWorldTransition.RollAngle / MathHelper.Pi, 0f, 1f);

            //缝线取焦点的实际投影，翻转期间收敛到屏幕中线（旋转枢轴），保证 θ=π 恒等
            Vector2 focusScreen = WorldToScreen(OniRainWorldTransition.FocusWorld);
            float pivotY = MathHelper.Clamp(focusScreen.Y / h, 0.3f, 0.7f);
            pivotY = MathHelper.Lerp(pivotY, 0.5f, rollProgress);

            //波前圆心取伞的水平位置
            float originU = MathHelper.Clamp(
                WorldToScreen(OniRainWorldTransition.UmbrellaWorld).X / w, -0.2f, 1.2f);

            //波前满值半径按实际宽高比铺满全屏，超宽屏不露边
            float relX = MathF.Max(MathF.Abs(originU), MathF.Abs(1f - originU)) * (w / h);
            float frontMax = MathF.Sqrt(relX * relX + 0.75f * 0.75f) + 0.15f;

            //镜中人影站在玩家倒影旁一步（垂直镜像下倒影就在玩家同列正对面），
            //中心压到缝下足够深，倒悬躯干不被缝线裁剪；异样只在驻留段闪现，
            //彼时 rollProgress=0，无需跟随翻转期的 x 收敛
            Player player = Main.LocalPlayer;
            Vector2 ghostUv = new(0.55f, pivotY + 0.2f);
            if (player?.active == true) {
                Vector2 pUv = WorldToScreen(player.Center) / new Vector2(w, h);
                ghostUv = new Vector2(pUv.X + 0.055f,
                    MathF.Max(2f * pivotY - pUv.Y, pivotY + 0.16f));
            }

            //水位线：从屏下 1.15 涨到枢轴；翻转期 rise=1，水位跟随枢轴向 0.5 收敛
            float waterLevel = MathHelper.Lerp(1.15f, pivotY,
                OniRainWorldTransition.RiseProgress);

            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            fx.Parameters["uPivotY"]?.SetValue(pivotY);
            fx.Parameters["uRollProgress"]?.SetValue(rollProgress);
            fx.Parameters["uOriginU"]?.SetValue(originU);
            fx.Parameters["uAspect"]?.SetValue(w / h);
            fx.Parameters["uFront"]?.SetValue(OniRainWorldTransition.Reveal * frontMax);
            fx.Parameters["uWaterLevel"]?.SetValue(waterLevel);
            fx.Parameters["uWaterWobble"]?.SetValue(OniRainWorldTransition.WaterWobble);
            fx.Parameters["uFoamBoost"]?.SetValue(OniRainWorldTransition.FoamBoost);
            fx.Parameters["uSwallow"]?.SetValue(OniRainWorldTransition.Swallow);
            fx.Parameters["uGrade"]?.SetValue(OniRainWorldTransition.Grade);
            fx.Parameters["uGlimpse"]?.SetValue(OniRainWorldTransition.Glimpse);
            fx.Parameters["uGlimpseRing"]?.SetValue(OniRainWorldTransition.GlimpseRingProgress);
            fx.Parameters["uGhostPos"]?.SetValue(ghostUv);
            fx.Parameters["uSeamGlow"]?.SetValue(OniRainWorldTransition.SeamGlow);
        }

        /// <summary>绕屏幕中心的旋转矩阵，覆盖缩放只在旋转中途起效，两端恒等</summary>
        private static Matrix BuildRollMatrix(float theta) {
            if (MathF.Abs(theta) <= 0.0001f) {
                return Matrix.Identity;
            }

            float w = Main.screenWidth;
            float h = Main.screenHeight;
            float c = MathF.Abs(MathF.Cos(theta));
            float s = MathF.Abs(MathF.Sin(theta));
            float cover = MathF.Max((w * c + h * s) / w, (w * s + h * c) / h);
            cover *= 1f + 0.03f * s;

            Vector2 pivot = new(w * 0.5f, h * 0.5f);
            return Matrix.CreateTranslation(-pivot.X, -pivot.Y, 0f)
                * Matrix.CreateRotationZ(theta)
                * Matrix.CreateScale(cover, cover, 1f)
                * Matrix.CreateTranslation(pivot.X, pivot.Y, 0f);
        }

        /// <summary>
        /// 旋转拖影：转得越快残影越长，位移随半径增大，枢轴处自然收敛（旋转运动模糊）
        /// </summary>
        private static void DrawRotationSmear(SpriteBatch spriteBatch, float theta) {
            float velocity = OniRainWorldTransition.RollVelocity;
            if (MathF.Abs(velocity) <= 0.004f) {
                return;
            }

            //两级滞后残影，加色低强度叠出角向涂抹
            DrawSmearTap(spriteBatch, theta - velocity * 2.4f, 0.15f);
            DrawSmearTap(spriteBatch, theta - velocity * 4.8f, 0.08f);
        }

        private static void DrawSmearTap(SpriteBatch spriteBatch, float lagTheta, float strength) {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, BuildRollMatrix(lagTheta));
            spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White * strength);
            spriteBatch.End();
        }

        /// <summary>结算白闪：灰白辉光罩 + 峰值处近全白</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch) {
            float flash = OniRainWorldTransition.Flash;
            if (flash <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            spriteBatch.Draw(white, full, new Color(0.82f, 0.90f, 0.92f, 0f) * (0.55f * flash));
            spriteBatch.End();

            if (flash > 0.55f) {
                float hard = (flash - 0.55f) / 0.45f;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                spriteBatch.Draw(white, full, new Color(226, 236, 238) * (0.92f * hard));
                spriteBatch.End();
            }
        }

        /// <summary>RT 不可用的纯色回退：按进度压暗 + 白闪，世界状态切换不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            //压暗跟涨水进度走，与主路径的水位上涨同节奏
            float dim = OniRainWorldTransition.RiseProgress * 0.28f
                + OniRainWorldTransition.Swallow * 0.20f;
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            if (dim > 0.002f) {
                spriteBatch.Draw(white, full, new Color(16, 20, 24) * dim);
            }
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch);
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
    }
}
