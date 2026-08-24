using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Melee.Arbiters
{
    /// <summary>
    /// 断罪师熔铸显现渲染:斧体走 ArbiterHellfire.fx TechForge(噪蚀成形+熔金垂丝+余温冷却),
    /// 底下垫汇聚烬流(确定性哈希驱动的加色拖曳),坠落期火尾、落地烬爆与拔斧迸裂在
    /// <see cref="UpdateAmbient"/>/<see cref="SpawnWrenchBurst"/> 派发;
    /// 着色器缺失回退普通精灵渐显
    /// </summary>
    internal static class ArbiterManifestationRenderer
    {
        private const int EmberStreamCount = 14;

        private static readonly Color HellDark = new(120, 35, 10);
        private static readonly Color HellMain = new(255, 120, 30);
        private static readonly Color HellEdge = new(255, 215, 130);

        private static bool renderFailureLogged;

        internal static void Draw(
            SpriteBatch spriteBatch,
            Texture2D axe,
            Vector2 worldCenter,
            float rotation,
            float scale,
            float manifestationProgress,
            float groundY,
            float heat) {

            if (Main.dedServ || spriteBatch == null || axe == null || axe.IsDisposed
                || !IsFinite(worldCenter) || !float.IsFinite(rotation) || !float.IsFinite(scale)
                || scale <= 0f || !float.IsFinite(manifestationProgress)
                || !float.IsFinite(groundY) || !float.IsFinite(heat)) {
                return;
            }

            float progress = MathHelper.Clamp(manifestationProgress, 0f, 1f);
            heat = MathHelper.Clamp(heat, 0f, 1f);

            //完全成形且余温散尽:普通精灵直绘,不再走着色器
            if (progress >= 0.999f && heat <= 0.01f) {
                spriteBatch.Draw(axe, worldCenter - Main.screenPosition, null,
                    GetLighting(worldCenter), rotation, axe.Size() * 0.5f,
                    scale, SpriteEffects.None, 0f);
                return;
            }

            Effect fx = EffectLoader.ArbiterHellfire?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (fx == null || noise == null || noise.IsDisposed || graphicsDevice == null) {
                DrawFallback(spriteBatch, axe, worldCenter, rotation, scale, progress);
                return;
            }

            Texture previousTexture1 = graphicsDevice.Textures[1];
            SamplerState previousSampler1 = graphicsDevice.SamplerStates[1];
            bool callerBatchEnded = false;
            bool workBatchOpen = false;
            bool actorBatchRestored = false;
            bool drawFallback = false;

            try {
                spriteBatch.End();
                callerBatchEnded = true;

                //汇聚烬流:锻造期从四周拖着尾焰扑向斧身
                if (progress < 0.97f) {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    workBatchOpen = true;
                    DrawEmberStreams(spriteBatch, worldCenter, rotation, scale, progress, groundY);
                    spriteBatch.End();
                    workBatchOpen = false;
                }

                //斧体熔铸
                float seed = groundY * 0.0071f + 0.37f;
                fx.CurrentTechnique = fx.Techniques["TechForge"];
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uForm"]?.SetValue(progress);
                fx.Parameters["uHeat"]?.SetValue(heat);
                fx.Parameters["uSeed"]?.SetValue(seed);
                fx.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    fx, Main.GameViewMatrix.TransformationMatrix);
                workBatchOpen = true;
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.Draw(axe, worldCenter - Main.screenPosition, null,
                    GetLighting(worldCenter), rotation, axe.Size() * 0.5f,
                    scale, SpriteEffects.None, 0f);

                spriteBatch.End();
                workBatchOpen = false;
            } catch (Exception exception) {
                drawFallback = true;
                LogRenderFailure(exception);
            } finally {
                if (workBatchOpen) {
                    TryEnd(spriteBatch);
                }

                graphicsDevice.Textures[1] = previousTexture1;
                graphicsDevice.SamplerStates[1] = previousSampler1;

                if (callerBatchEnded) {
                    actorBatchRestored = TryBeginActorBatch(spriteBatch);
                }
            }

            if (drawFallback && actorBatchRestored) {
                DrawFallback(spriteBatch, axe, worldCenter, rotation, scale, progress);
            }
        }

        /// <summary>确定性汇聚烬流:细尾焰沿弧线扑向锻造中的斧身,头亮尾散(加色批内)</summary>
        private static void DrawEmberStreams(SpriteBatch spriteBatch, Vector2 center,
            float rotation, float scale, float progress, float groundY) {

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }

            float anchorSeed = center.X * 0.0137f + groundY * 0.0079f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            //斧身长轴(纹理刃向-π/4 转到当前姿态)
            Vector2 bladeAxis = new Vector2(0.707f, -0.707f).RotatedBy(rotation);

            for (int i = 0; i < EmberStreamCount; i++) {
                float delay = Hash01(anchorSeed, i * 9 + 1) * 0.30f;
                float travelSpan = MathHelper.Lerp(0.40f, 0.66f, Hash01(anchorSeed, i * 9 + 2));
                float travel = MathHelper.Clamp((progress - delay) / travelSpan, 0f, 1f);
                float fade = SmoothStep(0f, 0.10f, travel)
                    * (1f - SmoothStep(0.74f, 1f, travel));
                if (travel <= 0f || fade <= 0.01f) {
                    continue;
                }

                float angle = Hash01(anchorSeed, i * 9 + 3) * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(200f, 380f, Hash01(anchorSeed, i * 9 + 4));
                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 target = center
                    + bladeAxis * MathHelper.Lerp(-46f, 48f, Hash01(anchorSeed, i * 9 + 5)) * scale;
                Vector2 chord = target - start;
                float side = Hash01(anchorSeed, i * 9 + 6) > 0.5f ? 1f : -1f;
                Vector2 control = (start + target) * 0.5f
                    + SafeNormalize(chord, Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                    * MathHelper.Lerp(70f, 150f, Hash01(anchorSeed, i * 9 + 7)) * side;

                float head = Smooth01(travel);
                Vector2 headPos = QuadraticBezier(start, control, target, head);
                Vector2 tangent = SafeNormalize(
                    QuadraticTangent(start, control, target, head), Vector2.UnitX);
                float streamRot = tangent.ToRotation();

                //尾焰:沿切向拉伸的暗橙拖曳(速度各向异性),头点白金过曝
                Color tail = Color.Lerp(HellDark, HellMain, 0.55f);
                float len = MathHelper.Lerp(0.9f, 1.6f, Hash01(anchorSeed, i * 9 + 8));
                spriteBatch.Draw(glow, headPos - Main.screenPosition, null,
                    tail * (fade * 0.55f), streamRot, glowOrigin,
                    new Vector2(len, 0.16f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, headPos - Main.screenPosition, null,
                    Color.Lerp(HellMain, HellEdge, head) * (fade * 0.8f), streamRot, glowOrigin,
                    new Vector2(0.34f, 0.12f), SpriteEffects.None, 0f);
            }

            //斧身底部热浪垫光:锻造中的熔炉腔
            float bodyGlow = SmoothStep(0.05f, 0.55f, progress) * 0.5f;
            if (bodyGlow > 0.02f) {
                spriteBatch.Draw(glow, center - Main.screenPosition, null,
                    HellMain * bodyGlow, 0f, glowOrigin,
                    new Vector2(2.6f, 2.2f) * scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, center - Main.screenPosition, null,
                    HellEdge * (bodyGlow * 0.5f), 0f, glowOrigin,
                    new Vector2(1.3f, 1.1f) * scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>逐帧环境派发:锻造期汇聚烬粒、坠落期火尾、落地拍烬爆</summary>
        internal static void UpdateAmbient(Vector2 axeCenter, ArbiterManifestationPhase phase,
            float progress, bool impact) {
            if (Main.dedServ || !IsFinite(axeCenter) || !float.IsFinite(progress)) {
                return;
            }

            progress = MathHelper.Clamp(progress, 0f, 1f);
            if (impact) {
                SpawnImpactBurst(axeCenter + new Vector2(0f, ArbiterManifestationActor.AxeCenterHeight));
            }

            //坠落火尾:速度拉伸的余烬甩在身后
            if (phase == ArbiterManifestationPhase.Falling) {
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = axeCenter + Main.rand.NextVector2Circular(14f, 20f);
                    Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 6f));
                    Color color = Color.Lerp(HellMain, HellEdge, Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(pos, vel, color,
                        Main.rand.NextFloat(0.7f, 1.3f)).Configure(false, Main.rand.Next(14, 26));
                }
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_LavaFire>(axeCenter + Main.rand.NextVector2Circular(12f, 16f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 2.5f)),
                        Color.White, Main.rand.NextFloat(0.7f, 1.1f));
                }
                return;
            }

            if (progress <= 0.01f || progress >= 0.97f) {
                return;
            }

            //锻造期:四周烬粒被拽向斧身
            float spawnChance = MathHelper.Lerp(0.34f, 0.12f, progress);
            if (Main.rand.NextFloat() > spawnChance) {
                return;
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(90f, 230f);
            Vector2 offset = angle.ToRotationVector2() * radius;
            Vector2 spawnPosition = axeCenter + offset;
            Vector2 target = axeCenter + Main.rand.NextVector2Circular(26f, 40f);
            int lifetime = Main.rand.Next(20, 32);
            Vector2 velocity = SafeNormalize(target - spawnPosition, Vector2.UnitY)
                * (radius / lifetime) * Main.rand.NextFloat(1.25f, 1.65f);
            Color emberColor = Color.Lerp(HellDark, HellMain, Main.rand.NextFloat(0.35f, 1f));
            PRTLoader.NewParticle<PRT_Spark>(spawnPosition, velocity, emberColor,
                Main.rand.NextFloat(0.55f, 1.05f)).Configure(false, lifetime);
        }

        /// <summary>落地烬爆:环形火星+上喷熔粒+崩起的碎石</summary>
        private static void SpawnImpactBurst(Vector2 impactCenter) {
            Color groundColor = GetLighting(impactCenter);
            for (int i = 0; i < 30; i++) {
                float horizontal = Main.rand.NextFloat(-7.5f, 7.5f);
                float vertical = -Main.rand.NextFloat(1.5f, 9f);
                Color color = Color.Lerp(HellMain, HellEdge, Main.rand.NextFloat(0.15f, 0.85f));
                PRTLoader.NewParticle<PRT_Spark>(
                    impactCenter + Main.rand.NextVector2Circular(14f, 5f),
                    new Vector2(horizontal, vertical), color,
                    Main.rand.NextFloat(0.7f, 1.5f)).Configure(i < 12, Main.rand.Next(22, 40));
            }

            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 4.5f));
                PRTLoader.NewParticle<PRT_LavaFire>(impactCenter + Main.rand.NextVector2Circular(22f, 6f),
                    vel, Color.White, Main.rand.NextFloat(0.9f, 1.5f));
            }

            for (int i = 0; i < 16; i++) {
                Vector2 velocity = new(Main.rand.NextFloat(-3.6f, 3.6f),
                    -Main.rand.NextFloat(0.5f, 5f));
                Dust dust = Dust.NewDustPerfect(impactCenter + Main.rand.NextVector2Circular(16f, 4f),
                    DustID.Stone, velocity, 90, groundColor, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = false;
            }
        }

        /// <summary>拔斧挣脱拍:土石与烬火自斧根迸起</summary>
        internal static void SpawnWrenchBurst(Vector2 groundPos) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1.5f, 6f));
                Dust dust = Dust.NewDustPerfect(groundPos + Main.rand.NextVector2Circular(14f, 4f),
                    DustID.Stone, vel, 90, GetLighting(groundPos), Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = false;
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 6.5f));
                Color color = Color.Lerp(HellMain, HellEdge, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(groundPos + Main.rand.NextVector2Circular(10f, 4f),
                    vel, color, Main.rand.NextFloat(0.6f, 1.2f)).Configure(false, Main.rand.Next(16, 28));
            }
        }

        private static void DrawFallback(SpriteBatch spriteBatch, Texture2D axe,
            Vector2 worldCenter, float rotation, float scale, float progress) {

            float reveal = SmoothStep(0.08f, 0.82f, progress);
            if (reveal <= 0.001f) {
                return;
            }

            spriteBatch.Draw(axe, worldCenter - Main.screenPosition, null,
                GetLighting(worldCenter) * reveal, rotation,
                axe.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        private static Color GetLighting(Vector2 worldPosition) {
            int maxTileX = Math.Max(Main.maxTilesX - 1, 0);
            int maxTileY = Math.Max(Main.maxTilesY - 1, 0);
            int tileX = Math.Clamp((int)(worldPosition.X / 16f), 0, maxTileX);
            int tileY = Math.Clamp((int)(worldPosition.Y / 16f), 0, maxTileY);
            return Lighting.GetColor(tileX, tileY);
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
            CWRMod.Instance.Logger.Warn(
                $"Arbiter manifestation renderer fallback: {exception.Message}");
        }

        private static Vector2 QuadraticBezier(Vector2 start, Vector2 control,
            Vector2 target, float amount) {
            float inverse = 1f - amount;
            return start * (inverse * inverse) + control * (2f * inverse * amount)
                + target * (amount * amount);
        }

        private static Vector2 QuadraticTangent(Vector2 start, Vector2 control,
            Vector2 target, float amount)
            => (control - start) * (2f * (1f - amount))
                + (target - control) * (2f * amount);

        private static Vector2 SafeNormalize(Vector2 value, Vector2 fallback) {
            float lengthSquared = value.LengthSquared();
            if (lengthSquared <= 0.0001f || !float.IsFinite(lengthSquared)) {
                return fallback;
            }
            return value / MathF.Sqrt(lengthSquared);
        }

        private static float Hash01(float seed, int index) {
            float value = MathF.Sin(seed + index * 12.9898f) * 43758.5453f;
            return value - MathF.Floor(value);
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
            => Smooth01((value - edge0) / Math.Max(edge1 - edge0, 0.0001f));

        private static bool IsFinite(Vector2 value)
            => float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
