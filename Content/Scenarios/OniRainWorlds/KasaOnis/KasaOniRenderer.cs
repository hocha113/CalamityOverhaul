using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis
{
    /// <summary>
    /// 伞鬼绘制：凝聚/消融期经 <c>OniSewage</c> 着色器（噪声侵蚀 + 地面裁切 + 垂滴扭曲），
    /// 行走期普通绘制加程序化蹒跚；脚下污潭用真 alpha 水渍贴图压扁铺开。<br/>
    /// 形制镜 <c>WGMaterializationRenderer</c>：Immediate 批次、s1 噪声、CPU 后备、恢复 Actor 批次。
    /// </summary>
    internal static class KasaOniRenderer
    {
        [VaultLoaden(CWRConstant.NPC + "OniRain/KasaOni")]
        private static Asset<Texture2D> KasaOniTex = null;
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> PuddleMask = null;

        private static bool renderFailureLogged;

        internal static void Draw(SpriteBatch spriteBatch, KasaOniActor oni) {
            Texture2D body = KasaOniTex?.Value;
            if (body == null || body.IsDisposed) {
                return;
            }

            KasaOniPhase phase = oni.Phase;
            float progress = MathHelper.Clamp(oni.CondenseProgress, 0f, 1f);

            DrawPuddle(spriteBatch, oni, phase, progress);

            if (phase == KasaOniPhase.Submerged) {
                return;
            }

            //身体统一以贴图中心锚定：脚底对齐 FeetAnchor（贴图脚在底边上方约4px）
            Vector2 feet = oni.FeetAnchor;
            Vector2 bodyCenter = feet - new Vector2(0f, body.Height * 0.5f - 4f);
            SpriteEffects flip = oni.FacingLeft
                ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color light = Lighting.GetColor((feet / 16f).ToPoint());
            //夜雨里保轮廓：环境光染向湿墨灰白
            light = Color.Lerp(light, KasaOniActor.PaleSheen, 0.30f);

            if (phase == KasaOniPhase.Walking) {
                DrawWalking(spriteBatch, oni, body, bodyCenter, light, flip);
                return;
            }

            //凝聚/消融走着色器；期间的轻微蠕动让液体感不僵
            float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f + oni.WhoAmI * 1.7f)
                * 0.035f * (1f - progress);
            DrawCondensing(spriteBatch, oni, body, bodyCenter, light, flip, progress, wobble);
        }

        /// <summary>行走：蹒跚摇摆 + 踏步压缩 + 冷灰青幽光衬底</summary>
        private static void DrawWalking(SpriteBatch spriteBatch, KasaOniActor oni,
            Texture2D body, Vector2 bodyCenter, Color light, SpriteEffects flip) {

            float moveFactor = MathHelper.Clamp(Math.Abs(oni.Velocity.X) / 1.15f, 0f, 1f);
            float rotation = MathF.Sin(oni.WaddlePhase) * 0.07f * moveFactor;
            float bob = Math.Abs(MathF.Sin(oni.WaddlePhase)) * 1.6f * moveFactor;
            Vector2 scale = new(1f + MathF.Sin(oni.WaddlePhase * 2f) * 0.015f * moveFactor,
                1f - Math.Abs(MathF.Sin(oni.WaddlePhase * 2f)) * 0.03f * moveFactor);
            Vector2 drawPos = bodyCenter - Main.screenPosition - new Vector2(0f, bob);

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + oni.WhoAmI) * 0.5f + 0.5f;
            Color backing = new Color(80, 102, 106) with { A = 0 } * (0.10f + pulse * 0.05f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f,
                new Vector2(110f / glow.Width, 96f / glow.Height), SpriteEffects.None, 0f);

            spriteBatch.Draw(body, drawPos, null, light, rotation,
                body.Size() * 0.5f, scale, flip, 0f);
        }

        /// <summary>凝聚/消融：OniSewage 着色器路径，异常回退普通淡入淡出</summary>
        private static void DrawCondensing(SpriteBatch spriteBatch, KasaOniActor oni,
            Texture2D body, Vector2 bodyCenter, Color light, SpriteEffects flip,
            float progress, float rotation) {

            Effect sewage = EffectLoader.OniSewage?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (sewage == null || noise == null || noise.IsDisposed || graphicsDevice == null) {
                DrawFallback(spriteBatch, body, bodyCenter, light, flip, progress);
                return;
            }

            Texture previousTexture1 = graphicsDevice.Textures[1];
            SamplerState previousSampler1 = graphicsDevice.SamplerStates[1];
            bool callerBatchEnded = false;
            bool sewageBatchOpen = false;
            bool actorBatchRestored = false;
            bool drawFallback = false;

            try {
                spriteBatch.End();
                callerBatchEnded = true;

                sewage.Parameters["uProgress"]?.SetValue(progress);
                sewage.Parameters["uTime"]?.SetValue(
                    Main.GlobalTimeWrappedHourly + oni.WhoAmI * 0.613f);
                sewage.Parameters["uTextureSize"]?.SetValue(new Vector2(body.Width, body.Height));
                sewage.Parameters["uScale"]?.SetValue(1f);
                sewage.Parameters["uRotation"]?.SetValue(rotation);
                sewage.Parameters["uCenterY"]?.SetValue(bodyCenter.Y);
                sewage.Parameters["uGroundY"]?.SetValue(oni.GroundLineY);
                sewage.Parameters["uSewageDeep"]?.SetValue(
                    KasaOniActor.SewageDeep.ToVector3());
                sewage.Parameters["uEdgeColor"]?.SetValue(
                    KasaOniActor.PaleSheen.ToVector3());

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    sewage, Main.GameViewMatrix.TransformationMatrix);
                sewageBatchOpen = true;
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.Draw(body, bodyCenter - Main.screenPosition, null, light,
                    rotation, body.Size() * 0.5f, 1f, flip, 0f);

                spriteBatch.End();
                sewageBatchOpen = false;
            } catch (Exception exception) {
                drawFallback = true;
                LogRenderFailure(exception);
            } finally {
                if (sewageBatchOpen) {
                    TryEnd(spriteBatch);
                }

                graphicsDevice.Textures[1] = previousTexture1;
                graphicsDevice.SamplerStates[1] = previousSampler1;

                if (callerBatchEnded) {
                    actorBatchRestored = TryBeginActorBatch(spriteBatch);
                }
            }

            if (drawFallback && actorBatchRestored) {
                DrawFallback(spriteBatch, body, bodyCenter, light, flip, progress);
            }
        }

        /// <summary>脚下污潭：凝聚期铺开又被吸干、消融期反向涨起，真 alpha 深色水渍</summary>
        private static void DrawPuddle(SpriteBatch spriteBatch, KasaOniActor oni,
            KasaOniPhase phase, float progress) {

            Texture2D mask = PuddleMask?.Value;
            if (mask == null) {
                return;
            }

            //包络：正弦弓形，0→张满→0；潜行期由冒泡粒子接管
            float envelope = phase switch {
                KasaOniPhase.Emerging => MathF.Sin(
                    MathHelper.Clamp(progress * 1.2f, 0f, 1f) * MathHelper.Pi),
                KasaOniPhase.Dissolving => MathF.Sin(
                    MathHelper.Clamp((1f - progress) * 1.2f, 0f, 1f) * MathHelper.Pi),
                _ => 0f,
            };
            if (envelope <= 0.03f) {
                return;
            }

            Vector2 feet = oni.FeetAnchor;
            Vector2 pos = feet - Main.screenPosition + new Vector2(0f, 3f);
            float wobble = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + oni.WhoAmI) * 0.06f;
            float width = MathHelper.Lerp(24f, 96f, envelope) * wobble;
            float height = MathHelper.Lerp(4f, 12f, envelope);
            Vector2 origin = mask.Size() * 0.5f;
            Vector2 scale = new(width / mask.Width, height / mask.Height);

            //深色浊底 + 尸斑青薄光沿
            spriteBatch.Draw(mask, pos, null, KasaOniActor.SewageDark * (0.72f * envelope),
                0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(mask, pos - new Vector2(0f, 1.5f), null,
                (KasaOniActor.CorpseTeal with { A = 0 }) * (0.22f * envelope),
                0f, origin, scale * new Vector2(0.82f, 0.55f), SpriteEffects.None, 0f);
        }

        /// <summary>着色器缺失/失败的后备：按凝聚度淡入淡出</summary>
        private static void DrawFallback(SpriteBatch spriteBatch, Texture2D body,
            Vector2 bodyCenter, Color light, SpriteEffects flip, float progress) {
            if (progress <= 0.01f) {
                return;
            }
            spriteBatch.Draw(body, bodyCenter - Main.screenPosition, null,
                light * progress, 0f, body.Size() * 0.5f, 1f, flip, 0f);
        }

        private static bool TryBeginActorBatch(SpriteBatch spriteBatch) {
            try {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                    null, Main.GameViewMatrix.TransformationMatrix);
                return true;
            } catch (Exception exception) {
                LogRenderFailure(exception);
                return false;
            }
        }

        private static void TryEnd(SpriteBatch spriteBatch) {
            try {
                spriteBatch.End();
            } catch (Exception exception) {
                LogRenderFailure(exception);
            }
        }

        private static void LogRenderFailure(Exception exception) {
            if (renderFailureLogged) {
                return;
            }
            renderFailureLogged = true;
            CWRMod.Instance.Logger.Warn($"KasaOni renderer fallback: {exception.Message}");
        }
    }
}
