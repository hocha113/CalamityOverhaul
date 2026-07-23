using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>领域 RenderHandle</summary>
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

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }
            OniDomainPlayer odp = OniDomain.Local;
            if (odp == null || !odp.GradeVisible) {
                return;
            }
            //RT 不可用时跳过世界浸染，整体走 EndCaptureDraw 的纯色叠层；鬼域不受领域简约偏好影响

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }

            Effect grade = EffectLoader.OniWorldGrade?.Value;
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

            SetSharedGradeParams(grade, odp);
            //浪头红烬、爆域最烈随扩散衰减，吸回时余温

            float frontEmber = 0f;
            if (odp.Phase == OniDomainPhase.Opening) {
                frontEmber = MathHelper.Lerp(1.0f, 0.3f, odp.SpreadProgress);
            }
            else if (odp.Phase == OniDomainPhase.Closing) {
                frontEmber = 0.3f;
            }
            grade.Parameters["uFrontEmber"]?.SetValue(frontEmber);

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
            OniDomainPlayer odp = OniDomain.Local;
            if (odp == null || !odp.GradeVisible) {
                return;
            }

            //鬼域不受领域简约偏好影响，仅技术性 RT 不可用时走低质量回退

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                odp.PendingPaperCapture = false;
                odp.PaperValid = false;
                DrawLowQualityFallback(spriteBatch, odp);
                DrawOverlays(spriteBatch, odp);
                return;
            }

            ApplyUnifyAndPeel(spriteBatch, graphicsDevice, screenSwap, odp);
            DrawOverlays(spriteBatch, odp);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu) {
                return;
            }
            OniDomainDeco.Draw(spriteBatch);
        }

        //两个 technique 共用的参数（世界选择/浸染前沿/死寂）

        private static void SetSharedGradeParams(Effect grade, OniDomainPlayer odp) {
            //捕获帧要以旧世界调色（WorldIsUra 已在状态机先行切换）

            bool gradeUra = odp.PendingPaperCapture ? !odp.WorldIsUra : odp.WorldIsUra;

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 spreadOrigin = Vector2.Transform(
                odp.EyeWorldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);

            float stillness = 0f;
            if (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.PreSilence) {
                stillness = MathHelper.Clamp(odp.PhaseTimer / 30f, 0f, 1f);
            }

            bool spread = odp.Phase == OniDomainPhase.Opening || odp.Phase == OniDomainPhase.Closing;

            grade.Parameters["uTime"]?.SetValue(odp.EffectTime);
            grade.Parameters["uScreenSize"]?.SetValue(screenSize);
            grade.Parameters["uWorldBlend"]?.SetValue(gradeUra ? 1f : 0f);
            grade.Parameters["uSpreadMode"]?.SetValue(spread ? 1f : 0f);
            grade.Parameters["uSpreadProgress"]?.SetValue(odp.SpreadProgress);
            grade.Parameters["uSpreadOrigin"]?.SetValue(spreadOrigin);
            grade.Parameters["uStillness"]?.SetValue(stillness);
        }

        private void ApplyUnifyAndPeel(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice,
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

            SetSharedGradeParams(grade, odp);
            grade.Parameters["uAnomalyPulse"]?.SetValue(odp.AnomalyPulse);
            grade.Parameters["uNegativeFlash"]?.SetValue(odp.NegativeFlash);

            //统一罩回写主屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            graphicsDevice.Textures[1] = noise;
            graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            grade.CurrentTechnique = grade.Techniques["TechUnify"];
            grade.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //捕获、此刻主屏已是"调色环境+清晰实体+统一罩"的完整旧世界成品帧，直接存作纸层

            if (odp.PendingPaperCapture) {
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
            //滑出、三次缓入，末段加速离场；两半按 PeelBias 不对称分滑

            float slide = prog * prog * (3f - 2f * prog);
            float slideDist = screenSize.Length() * 0.72f * slide * slide;
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
                //两半按 PeelBias 不对称分滑，每次翻转构图不同

                float bias = half == 0 ? odp.PeelBias : 1f - odp.PeelBias;
                peel.Parameters["uHalfSign"]?.SetValue(sign);
                peel.CurrentTechnique.Passes[0].Apply();
                Vector2 pos = center + nrm * sign * slideDist * bias;
                spriteBatch.Draw(paper, pos, null, Color.White,
                    sign * rot * (0.5f + bias), center, 1f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        private static void DrawOverlays(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            DrawOpeningDim(spriteBatch, odp);
            DrawBurstShockwave(spriteBatch, odp);
            DrawFlipSlashLine(spriteBatch, odp);
            DrawEyeOverlays(spriteBatch, odp);
        }

        //爆域冲击波、追着浸染前沿跑的红光环 + 头几帧的红闪

        private static void DrawBurstShockwave(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            if (odp.Phase != OniDomainPhase.Opening) {
                return;
            }
            int tBurst = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames + OniDomain.EyeBurstFrames;
            int st = odp.PhaseTimer - tBurst;
            if (st < 0 || st > 16) {
                return;
            }
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (ring == null || white == null) {
                return;
            }

            float f = st / 16f;
            Vector2 origin = Vector2.Transform(
                odp.EyeWorldPos - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
            float diag = new Vector2(Main.screenWidth, Main.screenHeight).Length();
            //环半径贴合浸染前沿（mask 前沿位于 dist≈progress*1.18）

            float radius = odp.SpreadProgress * 1.18f * diag + 50f;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //头 5 帧红闪

            if (st < 5) {
                float flashA = 0.30f * (1f - st / 5f);
                spriteBatch.Draw(white, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                    new Color(0.90f, 0.24f, 0.14f, 0f) * flashA);
            }

            Color ringCol = new Color(1f, 0.30f, 0.14f, 0f) * (0.55f * (1f - f));
            float scale = radius * 2f / ring.Width;
            spriteBatch.Draw(ring, origin, null, ringCol, 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            spriteBatch.End();
        }

        //鬼眼成形期间世界屏息压暗，爆域随扩散抬回

        private static void DrawOpeningDim(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            if (odp.Phase != OniDomainPhase.Opening) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }
            int tBurst = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames + OniDomain.EyeBurstFrames;
            float dim = odp.PhaseTimer <= tBurst
                ? odp.PhaseTimer / (float)tBurst
                : 1f - odp.SpreadProgress;
            if (dim <= 0.002f) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            Rectangle full = new(0, 0, Main.screenWidth, Main.screenHeight);
            spriteBatch.Draw(white, full, new Color(4, 2, 6) * (0.24f * dim));
            spriteBatch.End();
        }

        private static void DrawFlipSlashLine(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null || glow == null) {
                return;
            }

            float flipLine = 0f;
            if (odp.Phase == OniDomainPhase.Flipping) {
                if (odp.FlipStage == OniFlipStage.Flash) {
                    flipLine = 1f;
                }
                else if (odp.FlipStage == OniFlipStage.Peel) {
                    flipLine = MathHelper.Clamp(1f - odp.PeelProgress * 5f, 0f, 1f);
                }
            }
            if (flipLine <= 0.001f) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            //全屏斜断刀痕

            Vector2 center = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float diag = new Vector2(Main.screenWidth, Main.screenHeight).Length();
            DrawSlashLine(spriteBatch, white, glow, center,
                odp.FlipSlashAngle + MathHelper.PiOver2, diag * 1.25f, flipLine);

            spriteBatch.End();
        }

        private static void DrawEyeOverlays(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            Effect eye = EffectLoader.OniEye?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (eye == null || white == null || noise == null) {
                return;
            }

            //主眼，世界锚定；不限阶段，Omote 里的余韵也画

            if (odp.EyeVisible) {
                Vector2 pos = Vector2.Transform(
                    odp.EyeWorldPos - Main.screenPosition,
                    Main.GameViewMatrix.TransformationMatrix);
                //活体悬浮、轻微上下漂 + 睁眼撑大 + 爆闪鼓胀

                pos.Y += MathF.Sin(odp.EffectTime * 2.1f) * 3.5f;
                float halfSize = 118f * (0.88f + 0.12f * odp.EyeOpenAmount) + 16f * odp.EyeFlash;
                DrawEyeQuad(spriteBatch, eye, white, noise, pos, halfSize,
                    odp.EyeIntensity, odp.EyeOpenAmount, odp.EyeSpin, odp.EyeFlash,
                    odp.EyeDissolve, odp.UraSmooth, odp.EffectTime);
            }

            //负片帧彩蛋、旧世界的日/月化作同一只眼看你一眼

            if (odp.Phase == OniDomainPhase.Flipping && odp.FlipStage == OniFlipStage.Flash
                && odp.NegativeFlash > 0.01f) {
                float camX = Main.screenPosition.X;
                bool sun = odp.FlipToUra;
                //与 OniSky.fx 的天体位置常量保持一致

                Vector2 c = sun
                    ? new Vector2(0.310f - camX * 0.000010f, 0.560f)
                    : new Vector2(0.700f - camX * 0.000012f, 0.250f);
                Vector2 pos = new(c.X * Main.screenWidth, c.Y * Main.screenHeight);
                float bodyR = (sun ? 0.105f : 0.150f) * Main.screenHeight;
                //旧世界的天体化眼、表世界的日染绯红，里世界的月燃鬼火青

                DrawEyeQuad(spriteBatch, eye, white, noise, pos, bodyR * 2.3f,
                    odp.NegativeFlash, 1f, odp.EffectTime * 1.3f, 0f, 0f, sun ? 0f : 1f, odp.EffectTime);
            }
        }

        private static void DrawEyeQuad(SpriteBatch spriteBatch, Effect eye, Texture2D white, Texture2D noise,
            Vector2 center, float halfSize, float intensity, float open, float spin, float flash,
            float dissolve, float ura, float time) {

            var gd = Main.instance.GraphicsDevice;
            eye.Parameters["uTime"]?.SetValue(time);
            eye.Parameters["uIntensity"]?.SetValue(intensity);
            eye.Parameters["uOpen"]?.SetValue(open);
            eye.Parameters["uSpin"]?.SetValue(spin);
            eye.Parameters["uFlash"]?.SetValue(flash);
            eye.Parameters["uDissolve"]?.SetValue(dissolve);
            eye.Parameters["uUra"]?.SetValue(ura);
            //Effect 实例与 HUD 眼共享,参数会残留、世界眼必须显式回置笔宽增益

            eye.Parameters["uStrokeBoost"]?.SetValue(1f);

            Vector2 scale = new(halfSize * 2f / white.Width, halfSize * 2f / white.Height);
            Vector2 origin = white.Size() * 0.5f;

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //本体

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            eye.CurrentTechnique = eye.Techniques["TechEyeBase"];
            eye.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(white, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.End();

            //红光

            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            eye.CurrentTechnique = eye.Techniques["TechEyeGlow"];
            eye.CurrentTechnique.Passes[0].Apply();
            spriteBatch.Draw(white, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
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

        private static void DrawLowQualityFallback(SpriteBatch spriteBatch, OniDomainPlayer odp) {
            Texture2D white = VaultAsset.placeholder2?.Value;
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
