using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 鬼雨异化翻转的全屏合成：拷屏→镜面着色（血湖水线以下先行染向目标形态并沸腾搅动）→
    /// 绕屏幕中心旋转写回。几何契约与入雨演出一致：翻转期镜像 x 随 rollProgress
    /// 收敛为点反射，θ=π 时 180°翻转∘点反射=恒等、与真实渲染零跳变交接；
    /// 结算后 uSwallow 把镜面向上吞满全屏，uGrade 让位已切换的真实氛围。<br/>
    /// 对旁观者同样合成（缝线投影 clamp 0.3~0.7），但不锁输入不变焦。
    /// </summary>
    internal sealed class KikasaDomainFlipRender : RenderHandle
    {
        /// <summary>压在血湖领域(1.24)与入雨演出(2.0)之后，倒转是最后一道后处理</summary>
        public override float Weight => 2.02f;

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || kdp.Phase != KikasaDomainPhase.Flipping) {
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）走纯色低质量回退
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }

            Effect fx = EffectLoader.KikasaFlip?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                DrawLowQualityFallback(spriteBatch, kdp);
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
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //1. 拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //2. 镜面合成到第二交换屏
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            SetMirrorParams(fx, kdp);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechMirror"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //3. 绕屏幕中心旋转写回主屏，覆盖缩放保证旋转中途不露角
            float theta = kdp.FlipRollAngle;
            float coldMix = kdp.FlipToRain ? kdp.FlipMix : 1f - kdp.FlipMix;
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            //旋转途中的角落垫底色跟着目标形态走：血黑↔湿墨沉青
            graphicsDevice.Clear(Color.Lerp(new Color(16, 6, 8), new Color(10, 14, 17), coldMix));
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, BuildRollMatrix(theta));
            spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawRotationSmear(spriteBatch, kdp, theta);
            DrawFlashOverlay(spriteBatch, kdp);

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        private static void SetMirrorParams(Effect fx, KikasaDomainPlayer kdp) {
            float w = Main.screenWidth;
            float h = Main.screenHeight;
            float rollProgress = MathHelper.Clamp(kdp.FlipRollAngle / MathHelper.Pi, 0f, 1f);

            //缝线取湖面线的实际投影，翻转期间收敛到屏幕中线（旋转枢轴），保证 θ=π 恒等；
            //施术者有运镜把缝线压到中线，旁观者靠 clamp 兜底
            float pivotY = MathHelper.Clamp(
                WorldToScreen(new Vector2(Main.screenPosition.X, kdp.LakeWorldY)).Y / h,
                0.3f, 0.7f);
            pivotY = MathHelper.Lerp(pivotY, 0.5f, rollProgress);

            //沸腾隆起与异样涟漪环的圆心取施术者的水平位置
            float originU = MathHelper.Clamp(WorldToScreen(kdp.Player.Center).X / w, -0.2f, 1.2f);

            //倒悬伞影立在施术者倒影旁一步，伞盖远离缝线不被镜面遮罩裁剪
            Vector2 pUv = WorldToScreen(kdp.Player.Center) / new Vector2(w, h);
            Vector2 ghostUv = new(pUv.X + 0.06f, MathF.Max(2f * pivotY - pUv.Y, pivotY + 0.15f));

            float wobble = 0.0025f + 0.011f * kdp.FoamBoost + 0.010f * kdp.FlipBoil;
            float coldMix = kdp.FlipToRain ? kdp.FlipMix : 1f - kdp.FlipMix;
            //起手淡入：领域自带的湖面镜面先在场，合成层渐进接管避免交接跳变
            float fadeIn = MathHelper.Clamp(kdp.PhaseTimer / 14f, 0f, 1f);

            fx.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            fx.Parameters["uPivotY"]?.SetValue(pivotY);
            fx.Parameters["uRollProgress"]?.SetValue(rollProgress);
            fx.Parameters["uOriginU"]?.SetValue(originU);
            fx.Parameters["uAspect"]?.SetValue(w / h);
            fx.Parameters["uWaterLevel"]?.SetValue(pivotY);
            fx.Parameters["uWaterWobble"]?.SetValue(wobble);
            fx.Parameters["uFoamBoost"]?.SetValue(kdp.FoamBoost);
            fx.Parameters["uSwallow"]?.SetValue(kdp.FlipSwallow);
            fx.Parameters["uGrade"]?.SetValue(kdp.FlipGrade);
            fx.Parameters["uGlimpse"]?.SetValue(kdp.FlipGlimpse);
            fx.Parameters["uGlimpseRing"]?.SetValue(kdp.FlipGlimpseRing);
            fx.Parameters["uGhostPos"]?.SetValue(ghostUv);
            fx.Parameters["uSeamGlow"]?.SetValue(kdp.FlipSeamGlow);
            fx.Parameters["uBoil"]?.SetValue(kdp.FlipBoil);
            fx.Parameters["uColdMix"]?.SetValue(coldMix);
            fx.Parameters["uMix"]?.SetValue(fadeIn);
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

        /// <summary>旋转拖影：转得越快残影越长，位移随半径增大，枢轴处自然收敛</summary>
        private static void DrawRotationSmear(SpriteBatch spriteBatch, KikasaDomainPlayer kdp, float theta) {
            float velocity = kdp.FlipRollVelocity;
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

        /// <summary>结算白闪：辉光罩 + 峰值处近全白，色温随翻转方向走（入雨冷白/还血暖白）</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            float flash = kdp.FlipFlash;
            if (flash <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            Color soft = kdp.FlipToRain
                ? new Color(0.82f, 0.90f, 0.92f, 0f)
                : new Color(0.95f, 0.86f, 0.84f, 0f);
            Color hardCol = kdp.FlipToRain
                ? new Color(226, 236, 238)
                : new Color(240, 226, 224);

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

        /// <summary>RT 不可用的纯色回退：按沸腾/吞没进度压暗 + 白闪，形态切换不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float coldMix = kdp.FlipToRain ? kdp.FlipMix : 1f - kdp.FlipMix;
            float dim = kdp.FlipBoil * 0.20f + kdp.FlipSwallow * 0.20f;
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            if (dim > 0.002f) {
                spriteBatch.Draw(white, full,
                    Color.Lerp(new Color(24, 8, 10), new Color(16, 20, 24), coldMix) * dim);
            }
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch, kdp);
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
    }
}
