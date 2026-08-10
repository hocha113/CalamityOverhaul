using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖领域 RenderHandle。环境调色走 NPC 层之前的 TechGrade；
    /// EndCapture 走 TechUnify：全帧轻罩 + 血湖镜面（倒影含实体）+ 撕纸前沿。
    /// </summary>
    internal class KikasaDomainRender : RenderHandle
    {
        /// <summary>压在鬼域(1.2)之后、入雨演出(2.0)之前</summary>
        public override float Weight => 1.24f;

        public override void UpdateBySystem(int index) {
            //主菜单兜底清理（PostUpdateEverything 不再运行）

            if (Main.gameMenu) {
                KikasaDomainDeco.Clear();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.GradeVisible) {
                return;
            }
            //RT 不可用时跳过世界调色，整体走 EndCaptureDraw 的纯色叠层；血湖不受领域简约偏好影响

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }

            Effect grade = EffectLoader.KikasaGrade?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (grade == null || noise == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //拷屏到交换缓冲

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            SetSharedParams(grade, kdp);

            //调色回写主屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            grade.CurrentTechnique = grade.Techniques["TechGrade"];
            grade.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原 RT 绑定

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null || !kdp.GradeVisible) {
                return;
            }

            //仅技术性 RT 不可用时走低质量回退，不受领域简约偏好影响

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                DrawLowQualityFallback(spriteBatch, kdp);
                DrawSoakDim(spriteBatch, kdp);
                return;
            }

            ApplyUnify(spriteBatch, graphicsDevice, screenSwap, kdp);
            DrawSoakDim(spriteBatch, kdp);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainDeco.Draw(spriteBatch);
        }

        //两个 technique 共用的参数（撕纸遮罩/前沿/血湖）

        private static void SetSharedParams(Effect grade, KikasaDomainPlayer kdp) {
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 spreadOrigin = Vector2.Transform(
                kdp.OriginWorldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);

            bool spread = kdp.Phase == KikasaDomainPhase.Opening || kdp.Phase == KikasaDomainPhase.Closing;

            //前沿层可见度：收域末段淡出，Closed 瞬间不弹掉贴身湿渍
            float frontFade = 0f;
            if (kdp.Phase == KikasaDomainPhase.Opening) {
                frontFade = 1f;
            }
            else if (kdp.Phase == KikasaDomainPhase.Closing) {
                frontFade = MathHelper.Clamp(
                    (KikasaDomain.CloseFrames - 2 - kdp.PhaseTimer) / 8f, 0f, 1f);
            }

            //血湖水位：镜面绕 LakeWorldY 的稳定投影，涨水只推遮罩边界
            float pivotUv = MathHelper.Clamp(
                WorldToScreen(new Vector2(Main.screenPosition.X, kdp.LakeWorldY)).Y
                    / Main.screenHeight, -8f, 8f);
            float waterLevel = MathHelper.Lerp(1.15f, pivotUv, kdp.RiseProgress);
            float wobble = 0.0025f + 0.011f * kdp.FoamBoost;
            float seamGlow = MathHelper.Clamp(kdp.RiseProgress * 1.4f, 0f, 1f);

            grade.Parameters["uTime"]?.SetValue(kdp.EffectTime);
            grade.Parameters["uScreenSize"]?.SetValue(screenSize);
            grade.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            grade.Parameters["uSpreadProgress"]?.SetValue(kdp.SpreadProgress);
            grade.Parameters["uSpreadOrigin"]?.SetValue(spreadOrigin);
            grade.Parameters["uFrontFade"]?.SetValue(frontFade);
            grade.Parameters["uPivotY"]?.SetValue(pivotUv);
            grade.Parameters["uWaterLevel"]?.SetValue(waterLevel);
            grade.Parameters["uWaterWobble"]?.SetValue(wobble);
            grade.Parameters["uFoamBoost"]?.SetValue(kdp.FoamBoost);
            grade.Parameters["uSeamGlow"]?.SetValue(seamGlow);
            grade.Parameters["uAspect"]?.SetValue(screenSize.X / screenSize.Y);
        }

        private static void ApplyUnify(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap, KikasaDomainPlayer kdp) {

            Effect grade = EffectLoader.KikasaGrade?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (grade == null || noise == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch, kdp);
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //拷屏到交换缓冲

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            SetSharedParams(grade, kdp);

            //统一罩+血湖镜面回写主屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            grade.CurrentTechnique = grade.Techniques["TechUnify"];
            grade.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原 RT 绑定

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        //浸润屏息压暗，包络在玩家侧更新，中断时平滑退场

        private static void DrawSoakDim(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            if (kdp.SoakDim <= 0.002f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Draw(white, full, new Color(8, 2, 4) * (0.22f * kdp.SoakDim));
            spriteBatch.End();
        }

        private static void DrawLowQualityFallback(SpriteBatch spriteBatch, KikasaDomainPlayer kdp) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }

            float coverage = kdp.SpreadProgress;
            if (coverage <= 0.001f) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            //血暮轻罩 + 沉暗，血湖细节让位

            spriteBatch.Draw(white, full, new Color(96, 18, 20) * (0.22f * coverage));
            spriteBatch.Draw(white, full, new Color(14, 4, 8) * (0.16f * coverage));

            spriteBatch.End();
        }

        private static Vector2 WorldToScreen(Vector2 worldPos)
            => Vector2.Transform(worldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
    }
}
