using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 湿墨冲刷的全屏合成：拷屏→湿墨冲刷（颜色向下流淌溶解）+ 雨帘合拢
    /// （满幕遮蔽藏结算）+ 排墨揭新层，一趟 pass 写回。<br/>
    /// 深潜（<see cref="OniRainDescentTransition"/>）与送出（<see cref="OniRainExitTransition"/>）
    /// 共用这条管线，两条演出互斥，按活动源取包络；
    /// 包络全零时输出恒等输入，与 <see cref="OniRainWorldRender"/> 的翻转合成活动互斥。
    /// </summary>
    internal sealed class OniRainDescentRender : RenderHandle
    {
        /// <summary>排在翻转合成之后收尾，两者互斥不会同帧生效</summary>
        public override float Weight => 2.05f;

        //当前活动冲刷源的包络快照，ResolveWashSource 每帧填充
        private static float srcInkRun;
        private static float srcCover;
        private static float srcDrain;
        private static float srcFlash;
        private static Vector2 srcOrigin;

        /// <summary>按活动演出取冲刷包络：深潜优先，其次送出；都不活动返回 false</summary>
        private static bool ResolveWashSource() {
            if (OniRainDescentTransition.RenderActive) {
                srcInkRun = OniRainDescentTransition.InkRun;
                srcCover = OniRainDescentTransition.CurtainCover;
                srcDrain = OniRainDescentTransition.Drain;
                srcFlash = OniRainDescentTransition.Flash;
                srcOrigin = OniRainDescentTransition.UmbrellaWorld;
                return true;
            }
            if (OniRainExitTransition.RenderActive) {
                srcInkRun = OniRainExitTransition.InkRun;
                srcCover = OniRainExitTransition.CurtainCover;
                srcDrain = OniRainExitTransition.Drain;
                srcFlash = OniRainExitTransition.Flash;
                srcOrigin = OniRainExitTransition.FocusWorld;
                return true;
            }
            return false;
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu || !ResolveWashSource()) {
                return;
            }

            //技术性 RT 不可用（Retro/Trippy 光照等）走纯色低质量回退
            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch);
                return;
            }

            Effect fx = EffectLoader.OniRainDescent?.Value;
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

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //1. 拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //2. 冲刷合成写回主屏
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(new Color(10, 14, 17));
            SetDescentParams(fx);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            fx.CurrentTechnique = fx.Techniques["TechDescent"];
            fx.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            DrawFlashOverlay(spriteBatch);

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        private static void SetDescentParams(Effect fx) {
            float w = Main.screenWidth;
            float h = Main.screenHeight;

            //排墨撕口圆心取演出焦点的水平位置（深潜=伞，送出=人）
            float originU = MathHelper.Clamp(
                WorldToScreen(srcOrigin).X / w, -0.2f, 1.2f);

            fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            fx.Parameters["uInkRun"]?.SetValue(srcInkRun);
            fx.Parameters["uCover"]?.SetValue(srcCover);
            fx.Parameters["uDrain"]?.SetValue(srcDrain);
            fx.Parameters["uFlash"]?.SetValue(srcFlash);
            fx.Parameters["uOriginU"]?.SetValue(originU);
            fx.Parameters["uAspect"]?.SetValue(w / h);
        }

        /// <summary>结算雷闪：灰白辉光罩 + 峰值处近全白，与入雨演出同一语汇</summary>
        private static void DrawFlashOverlay(SpriteBatch spriteBatch) {
            float flash = srcFlash;
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
            spriteBatch.Draw(white, full, new Color(0.82f, 0.90f, 0.92f, 0f) * (0.50f * flash));
            spriteBatch.End();

            if (flash > 0.55f) {
                float hard = (flash - 0.55f) / 0.45f;
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                spriteBatch.Draw(white, full, new Color(226, 236, 238) * (0.90f * hard));
                spriteBatch.End();
            }
        }

        /// <summary>RT 不可用的纯色回退：合幕压暗 + 排墨回亮 + 雷闪，状态切换不受影响</summary>
        private static void DrawLowQualityFallback(SpriteBatch spriteBatch) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            //压暗跟合幕走，排墨段随排尽回亮
            float dim = (srcCover * 0.32f + srcInkRun * 0.12f) * (1f - srcDrain);
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
