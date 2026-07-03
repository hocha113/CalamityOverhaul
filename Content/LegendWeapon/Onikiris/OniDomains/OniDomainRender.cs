using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    /// <summary>
    /// 鬼域 RenderHandle：全屏调色、翻转纸层捕获与剥落、开域白线、低画质回退
    /// <br/>ScreenTargets[0] 为翻转纸层（捕获的旧世界画面）
    /// </summary>
    internal class OniDomainRender : RenderHandle
    {
        public override int ScreenSlot => 1;

        public override float Weight => 1.2f;

        public override void UpdateBySystem(int index) {
            //主菜单兜底清理（PostUpdateEverything 不再运行）
            if (Main.gameMenu) {
                OniDomainDeco.Clear();
            }
        }

        public override void EndCaptureDraw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            OniDomainPlayer odp = OniDomain.Local;
            if (odp == null || !odp.GradeVisible) {
                return;
            }

            if (RenderQualitySafety.NeedsScreenTargetFallback()) {
                odp.PendingPaperCapture = false;
                odp.PaperValid = false;
                DrawLowQualityFallback(spriteBatch, odp);
                DrawSlashOverlays(spriteBatch, odp);
                return;
            }

            ApplyGradeAndPeel(spriteBatch, graphicsDevice, screenSwap, odp);
            DrawSlashOverlays(spriteBatch, odp);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }
            OniDomainDeco.Draw(spriteBatch);
        }

        //====== 全屏调色 + 纸层剥落 ======

        private void ApplyGradeAndPeel(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
            RenderTarget2D screenSwap, OniDomainPlayer odp) {

            Effect grade = EffectLoader.OniWorldGrade?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (grade == null || noise == null) return;
            if (screenSwap == null || screenSwap.IsDisposed) return;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return;
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                DrawLowQualityFallback(spriteBatch, odp);
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //拷屏到交换缓冲
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //捕获帧要以旧世界调色（WorldIsUra 已在状态机先行切换）
            bool capturing = odp.PendingPaperCapture;
            bool gradeUra = capturing ? !odp.WorldIsUra : odp.WorldIsUra;

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 slashScreen = Vector2.Transform(
                odp.SlashWorldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);

            float stillness = 0f;
            if (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.PreSilence) {
                stillness = MathHelper.Clamp(odp.PhaseTimer / 45f, 0f, 1f);
            }

            bool spread = odp.Phase == OniDomainPhase.Opening || odp.Phase == OniDomainPhase.Closing;

            grade.Parameters["uTime"]?.SetValue(odp.EffectTime);
            grade.Parameters["uScreenSize"]?.SetValue(screenSize);
            grade.Parameters["uWorldBlend"]?.SetValue(gradeUra ? 1f : 0f);
            grade.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            grade.Parameters["uSpreadProgress"]?.SetValue(odp.SpreadProgress);
            grade.Parameters["uSlashScreenPos"]?.SetValue(slashScreen);
            grade.Parameters["uAnomalyPulse"]?.SetValue(odp.AnomalyPulse);
            grade.Parameters["uNegativeFlash"]?.SetValue(odp.NegativeFlash);
            grade.Parameters["uStillness"]?.SetValue(stillness);

            //调色回写主屏
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            grade.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //捕获：把刚调好色的旧世界画面存作纸层
            if (capturing) {
                RenderTarget2D paper = GetPaperTarget();
                if (paper != null) {
                    graphicsDevice.SetRenderTarget(paper);
                    graphicsDevice.Clear(Color.Transparent);
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
                    spriteBatch.End();
                    graphicsDevice.SetRenderTarget(Main.screenTarget);
                    odp.PaperValid = true;
                }
                odp.PendingPaperCapture = false;
            }

            //纸层剥落
            if (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.Peel && odp.PaperValid) {
                DrawPaperPeel(spriteBatch, graphicsDevice, odp);
            }

            //还原 RT 绑定
            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        private RenderTarget2D GetPaperTarget() {
            if (ScreenTargets == null || ScreenTargets.Length < 1) {
                return null;
            }
            RenderTarget2D paper = ScreenTargets[0];
            if (paper == null || paper.IsDisposed) {
                return null;
            }
            //分辨率变化后尺寸不符则放弃本次翻转纸层
            if (paper.Width != Main.screenTarget.Width || paper.Height != Main.screenTarget.Height) {
                return null;
            }
            return paper;
        }

        private void DrawPaperPeel(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, OniDomainPlayer odp) {
            Effect peel = EffectLoader.OniPaperPeel?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            RenderTarget2D paper = GetPaperTarget();
            if (peel == null || noise == null || paper == null) {
                odp.PaperValid = false;
                return;
            }

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 center = screenSize * 0.5f;
            Vector2 dir = odp.FlipSlashAngle.ToRotationVector2();
            Vector2 nrm = new(-dir.Y, dir.X);
            float prog = odp.PeelProgress;
            //滑出：三次缓入，末段加速离场
            float slide = prog * prog * (3f - 2f * prog);
            float slideDist = screenSize.Length() * 0.36f * slide * slide;
            float rot = 0.085f * slide;

            peel.Parameters["uTime"]?.SetValue(odp.EffectTime);
            peel.Parameters["uPeelProgress"]?.SetValue(prog);
            peel.Parameters["uScreenSize"]?.SetValue(screenSize);
            peel.Parameters["uSlashPoint"]?.SetValue(center);
            peel.Parameters["uSlashDir"]?.SetValue(dir);

            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            for (int half = 0; half < 2; half++) {
                float sign = half == 0 ? 1f : -1f;
                peel.Parameters["uHalfSign"]?.SetValue(sign);
                peel.CurrentTechnique.Passes[0].Apply();
                Vector2 pos = center + nrm * sign * slideDist;
                spriteBatch.Draw(paper, pos, null, Color.White, sign * rot, center, 1f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        //====== 白线开域刀痕 / 全屏翻转刀痕 ======

        private static void DrawSlashOverlays(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null || glow == null) {
                return;
            }

            float openLine = odp.SlashLineIntensity;
            float flipLine = 0f;
            if (odp.Phase == OniDomainPhase.Flipping) {
                if (odp.FlipStage == OniFlipStage.Flash) {
                    flipLine = 1f;
                }
                else if (odp.FlipStage == OniFlipStage.Peel) {
                    flipLine = MathHelper.Clamp(1f - odp.PeelProgress * 5f, 0f, 1f);
                }
            }
            if (openLine <= 0.001f && flipLine <= 0.001f) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (openLine > 0.001f) {
                //世界裂口：竖直微斜白线，出现时抽长
                Vector2 pos = Vector2.Transform(
                    odp.SlashWorldPos - Main.screenPosition,
                    Main.GameViewMatrix.TransformationMatrix);
                float ease = 1f - (1f - openLine) * (1f - openLine);
                float len = MathHelper.Lerp(26f, 200f, ease);
                float flicker = 0.9f + 0.1f * MathF.Sin(odp.EffectTime * 37f);
                float angle = -0.13f;
                DrawSlashLine(spriteBatch, white, glow, pos, angle, len, openLine * flicker);
            }

            if (flipLine > 0.001f) {
                //全屏斜断刀痕
                Vector2 center = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                float diag = new Vector2(Main.screenWidth, Main.screenHeight).Length();
                DrawSlashLine(spriteBatch, white, glow, center,
                    odp.FlipSlashAngle + MathHelper.PiOver2, diag * 1.25f, flipLine);
            }

            spriteBatch.End();
        }

        private static void DrawSlashLine(SpriteBatch spriteBatch, Texture2D white, Texture2D glow,
            Vector2 pos, float angle, float length, float intensity) {

            Vector2 whiteOrigin = white.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            //宽晕
            Color haze = new Color(0.75f, 0.72f, 0.85f, 0f) * (0.35f * intensity);
            spriteBatch.Draw(glow, pos, null, haze, angle, glowOrigin,
                new Vector2(26f / glow.Width, length * 1.06f / glow.Height), SpriteEffects.None, 0f);

            //中层
            Color mid = new Color(0.9f, 0.88f, 0.95f, 0f) * (0.55f * intensity);
            spriteBatch.Draw(white, pos, null, mid, angle, whiteOrigin,
                new Vector2(5f / white.Width, length / white.Height), SpriteEffects.None, 0f);

            //白芯
            Color core = Color.White * intensity;
            spriteBatch.Draw(white, pos, null, core, angle, whiteOrigin,
                new Vector2(2f / white.Width, length * 0.96f / white.Height), SpriteEffects.None, 0f);
        }

        //====== 低画质回退：纯色叠层 ======

        private static void DrawLowQualityFallback(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            if (white == null) {
                return;
            }

            float coverage = odp.SpreadProgress;
            if (coverage <= 0.001f) {
                return;
            }

            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (odp.WorldIsUra) {
                spriteBatch.Draw(white, full, new Color(8, 8, 14) * (0.52f * coverage));
                spriteBatch.Draw(white, full, new Color(60, 8, 10) * (0.10f * coverage));
            }
            else {
                spriteBatch.Draw(white, full, new Color(226, 204, 152) * (0.16f * coverage));
                spriteBatch.Draw(white, full, new Color(40, 30, 16) * (0.10f * coverage));
            }

            if (odp.NegativeFlash > 0.01f) {
                spriteBatch.Draw(white, full, new Color(235, 232, 240) * (0.75f * odp.NegativeFlash));
            }

            spriteBatch.End();
        }
    }
}
