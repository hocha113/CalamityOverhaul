using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses
{
    internal static class WGMaterializationRenderer
    {
        private const int SoulCount = 21;
        private const int SoulSeedBatches = 3;
        private const int SoulsPerBatch = SoulCount / SoulSeedBatches;
        private const int FilamentCount = 12;
        private const int FilamentSamples = 18;

        private static readonly Color SoulDark = new(100, 43, 69);
        private static readonly Color SoulMain = new(200, 111, 145);
        private static readonly Color SoulEdge = new(255, 205, 222);
        private static readonly Vector2 NativeSwordAxis = new(0.663f, -0.749f);
        private static readonly VertexPositionColorTexture[] VortexVertices = new VertexPositionColorTexture[4];
        private static readonly VertexPositionColorTexture[] WraithVertices
            = new VertexPositionColorTexture[SoulsPerBatch * 6];
        private static readonly VertexPositionColorTexture[] FilamentVertices
            = new VertexPositionColorTexture[FilamentSamples * 2];

        private static bool renderFailureLogged;

        internal static void Draw(
            SpriteBatch spriteBatch,
            Texture2D sword,
            Vector2 worldCenter,
            float rotation,
            float scale,
            float manifestationProgress,
            float groundY,
            float opacity = 1f) {

            if (Main.dedServ || spriteBatch == null || sword == null || sword.IsDisposed
                || !IsFinite(worldCenter) || !float.IsFinite(rotation) || !float.IsFinite(scale)
                || scale <= 0f || !float.IsFinite(manifestationProgress)
                || !float.IsFinite(groundY) || !float.IsFinite(opacity)) {
                return;
            }

            float progress = MathHelper.Clamp(manifestationProgress, 0f, 1f);
            opacity = MathHelper.Clamp(opacity, 0f, 1f);
            if (opacity <= 0.001f) {
                return;
            }

            Effect materialize = EffectLoader.WeaverMaterialize?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            GraphicsDevice graphicsDevice = Main.instance?.GraphicsDevice;
            if (materialize == null || noise == null || noise.IsDisposed || graphicsDevice == null) {
                DrawFallback(spriteBatch, sword, worldCenter, rotation, scale, progress, opacity);
                return;
            }

            Texture previousTexture1 = graphicsDevice.Textures[1];
            SamplerState previousSampler1 = graphicsDevice.SamplerStates[1];
            bool callerBatchEnded = false;
            bool materialBatchOpen = false;
            bool actorBatchRestored = false;
            bool drawFallback = false;

            try {
                spriteBatch.End();
                callerBatchEnded = true;

                DrawConvergence(graphicsDevice, noise, worldCenter, rotation, scale,
                    progress, groundY, opacity);

                ConfigureMaterializeEffect(materialize, sword, worldCenter, rotation,
                    scale, progress, groundY);
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    materialize, Main.GameViewMatrix.TransformationMatrix);
                materialBatchOpen = true;
                graphicsDevice.Textures[1] = noise;
                graphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                spriteBatch.Draw(sword, worldCenter - Main.screenPosition, null,
                    GetLighting(worldCenter) * opacity, rotation, sword.Size() * 0.5f,
                    scale, SpriteEffects.None, 0f);

                spriteBatch.End();
                materialBatchOpen = false;
            } catch (Exception exception) {
                drawFallback = true;
                LogRenderFailure(exception);
            } finally {
                if (materialBatchOpen) {
                    TryEnd(spriteBatch);
                }

                graphicsDevice.Textures[1] = previousTexture1;
                graphicsDevice.SamplerStates[1] = previousSampler1;

                if (callerBatchEnded) {
                    actorBatchRestored = TryBeginActorBatch(spriteBatch);
                }
            }

            if (drawFallback && actorBatchRestored) {
                DrawFallback(spriteBatch, sword, worldCenter, rotation, scale, progress, opacity);
            }
        }

        internal static void UpdateAmbient(Vector2 swordCenter, float progress, bool impact) {
            if (Main.dedServ || !IsFinite(swordCenter) || !float.IsFinite(progress)) {
                return;
            }

            progress = MathHelper.Clamp(progress, 0f, 1f);
            if (impact) {
                SpawnImpactBurst(swordCenter + new Vector2(0f, 64f));
            }

            if (progress <= 0.01f || progress >= 0.97f) {
                return;
            }

            float spawnChance = MathHelper.Lerp(0.30f, 0.10f, progress);
            if (Main.rand.NextFloat() > spawnChance) {
                return;
            }

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(90f, 225f);
            Vector2 offset = angle.ToRotationVector2() * radius;
            Vector2 spawnPosition = swordCenter + offset;
            Vector2 target = swordCenter + Main.rand.NextVector2Circular(20f, 54f);
            int lifetime = Main.rand.Next(20, 32);
            Vector2 velocity = SafeNormalize(target - spawnPosition, Vector2.UnitY)
                * (radius / lifetime) * Main.rand.NextFloat(1.25f, 1.65f);
            Color color = Color.Lerp(SoulDark, SoulMain, Main.rand.NextFloat(0.35f, 1f));
            PRTLoader.NewParticle<PRT_Spark>(spawnPosition, velocity, color,
                Main.rand.NextFloat(0.55f, 1.05f)).Configure(false, lifetime);
        }

        private static void DrawConvergence(GraphicsDevice graphicsDevice, Texture2D noise,
            Vector2 worldCenter, float rotation, float scale, float progress,
            float groundY, float opacity) {

            BlendState previousBlend = graphicsDevice.BlendState;
            RasterizerState previousRasterizer = graphicsDevice.RasterizerState;
            DepthStencilState previousDepth = graphicsDevice.DepthStencilState;
            try {
                graphicsDevice.BlendState = BlendState.AlphaBlend;
                graphicsDevice.RasterizerState = RasterizerState.CullNone;
                graphicsDevice.DepthStencilState = DepthStencilState.None;

                Matrix transform = VaultUtils.GetTransfromMatrix();
                float anchorSeed = worldCenter.X * 0.0137f + groundY * 0.0079f;
                DrawVortex(graphicsDevice, noise, transform, worldCenter, progress,
                    opacity, anchorSeed);
                DrawFilaments(graphicsDevice, noise, transform, worldCenter, rotation,
                    scale, progress, opacity, anchorSeed);
                DrawWraiths(graphicsDevice, noise, transform, worldCenter, rotation,
                    scale, progress, opacity, anchorSeed);
            } finally {
                graphicsDevice.BlendState = previousBlend;
                graphicsDevice.RasterizerState = previousRasterizer;
                graphicsDevice.DepthStencilState = previousDepth;
            }
        }

        private static void DrawVortex(GraphicsDevice graphicsDevice, Texture2D noise,
            Matrix transform, Vector2 center, float progress, float opacity, float anchorSeed) {

            Effect effect = EffectLoader.WeaverSoulVortex?.Value;
            float fade = SmoothStep(0.01f, 0.12f, progress)
                * (1f - SmoothStep(0.70f, 0.96f, progress)) * opacity;
            if (effect == null || fade <= 0.002f) {
                return;
            }

            float halfSize = MathHelper.Lerp(285f, 92f, Smooth01(progress));
            SetQuad(VortexVertices, center, Vector2.UnitX, Vector2.UnitY, halfSize, halfSize, Color.White);

            effect.Parameters["transformMatrix"]?.SetValue(transform);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + anchorSeed * 0.017f);
            effect.Parameters["uFade"]?.SetValue(fade * 0.72f);
            effect.Parameters["uSpinDir"]?.SetValue(Hash01(anchorSeed, 43) > 0.5f ? 1f : -1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip,
                    VortexVertices, 0, 2);
            }
        }

        private static void DrawFilaments(GraphicsDevice graphicsDevice, Texture2D noise,
            Matrix transform, Vector2 center, float rotation, float scale, float progress,
            float opacity, float anchorSeed) {

            Effect effect = EffectLoader.WeaverSlashTrail?.Value;
            float supportFade = SmoothStep(0.02f, 0.11f, progress)
                * (1f - SmoothStep(0.78f, 0.98f, progress)) * opacity;
            if (effect == null || supportFade <= 0.002f) {
                return;
            }

            effect.Parameters["transformMatrix"]?.SetValue(transform);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + anchorSeed * 0.009f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            Vector2 bladeAxis = NativeSwordAxis.RotatedBy(rotation);
            Vector2 bladeNormal = bladeAxis.RotatedBy(MathHelper.PiOver2);
            for (int filament = 0; filament < FilamentCount; filament++) {
                float delay = Hash01(anchorSeed, filament * 7 + 1) * 0.24f;
                float travelSpan = MathHelper.Lerp(0.48f, 0.70f,
                    Hash01(anchorSeed, filament * 7 + 2));
                float travel = MathHelper.Clamp((progress - delay) / travelSpan, 0f, 1f);
                float filamentFade = SmoothStep(0f, 0.10f, travel)
                    * (1f - SmoothStep(0.78f, 1f, travel)) * supportFade;
                if (travel <= 0f || filamentFade <= 0.002f) {
                    continue;
                }

                float angle = Hash01(anchorSeed, filament * 7 + 3) * MathHelper.TwoPi;
                float radius = MathHelper.Lerp(190f, 365f,
                    Hash01(anchorSeed, filament * 7 + 4));
                Vector2 start = center + angle.ToRotationVector2() * radius;
                Vector2 target = center
                    + bladeAxis * MathHelper.Lerp(-72f, 74f,
                        Hash01(anchorSeed, filament * 7 + 5)) * scale
                    + bladeNormal * MathHelper.Lerp(-7f, 7f,
                        Hash01(anchorSeed, filament * 7 + 6)) * scale;
                Vector2 chord = target - start;
                float side = Hash01(anchorSeed, filament * 7 + 7) > 0.5f ? 1f : -1f;
                Vector2 control = (start + target) * 0.5f
                    + SafeNormalize(chord, Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                    * MathHelper.Lerp(80f, 170f, Hash01(anchorSeed, filament * 7 + 8)) * side;

                float head = Smooth01(travel);
                float trailLength = MathHelper.Lerp(0.20f, 0.40f,
                    Hash01(anchorSeed, filament * 7 + 9));
                float tail = Math.Max(0f, head - trailLength);
                FillFilament(start, control, target, tail, head, scale);

                effect.Parameters["uFade"]?.SetValue(filamentFade * 0.86f);
                effect.Parameters["uHeat"]?.SetValue(MathHelper.Lerp(0.06f, 0.14f,
                    Hash01(anchorSeed, filament * 7 + 10)));
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip,
                        FilamentVertices, 0, FilamentVertices.Length - 2);
                }
            }
        }

        private static void DrawWraiths(GraphicsDevice graphicsDevice, Texture2D noise,
            Matrix transform, Vector2 center, float rotation, float scale, float progress,
            float opacity, float anchorSeed) {

            Effect effect = EffectLoader.WeaverWraith?.Value;
            float supportFade = SmoothStep(0.01f, 0.10f, progress)
                * (1f - SmoothStep(0.80f, 0.99f, progress)) * opacity;
            if (effect == null || supportFade <= 0.002f) {
                return;
            }

            effect.Parameters["transformMatrix"]?.SetValue(transform);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + anchorSeed * 0.011f);
            effect.Parameters["uFade"]?.SetValue(1f);
            effect.Parameters["uRage"]?.SetValue(0f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            Vector2 bladeAxis = NativeSwordAxis.RotatedBy(rotation);
            Vector2 bladeNormal = bladeAxis.RotatedBy(MathHelper.PiOver2);
            for (int batch = 0; batch < SoulSeedBatches; batch++) {
                int vertexCount = 0;
                for (int slot = 0; slot < SoulsPerBatch; slot++) {
                    int soulIndex = batch + slot * SoulSeedBatches;
                    if (!TryGetSoulPose(center, bladeAxis, bladeNormal, scale, progress,
                        supportFade, anchorSeed, soulIndex, out Vector2 position,
                        out Vector2 direction, out float fade, out float soulScale)) {
                        continue;
                    }

                    float halfWidth = 48f * soulScale;
                    float halfHeight = 31f * soulScale;
                    AddWraithQuad(WraithVertices, ref vertexCount, position, direction,
                        halfWidth, halfHeight, Color.White * fade);
                }

                if (vertexCount == 0) {
                    continue;
                }

                effect.Parameters["uSeed"]?.SetValue(anchorSeed * 0.019f + batch * 1.731f);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,
                        WraithVertices, 0, vertexCount / 3);
                }
            }
        }

        private static bool TryGetSoulPose(Vector2 center, Vector2 bladeAxis,
            Vector2 bladeNormal, float scale, float progress, float supportFade,
            float anchorSeed, int soulIndex, out Vector2 position, out Vector2 direction,
            out float fade, out float soulScale) {

            float delay = Hash01(anchorSeed, soulIndex * 11 + 1) * 0.30f;
            float travelSpan = MathHelper.Lerp(0.42f, 0.68f,
                Hash01(anchorSeed, soulIndex * 11 + 2));
            float travel = MathHelper.Clamp((progress - delay) / travelSpan, 0f, 1f);
            fade = SmoothStep(0f, 0.09f, travel)
                * (1f - SmoothStep(0.72f, 1f, travel)) * supportFade;
            if (travel <= 0f || fade <= 0.002f) {
                position = default;
                direction = default;
                soulScale = 0f;
                return false;
            }

            float angle = Hash01(anchorSeed, soulIndex * 11 + 3) * MathHelper.TwoPi;
            float radius = MathHelper.Lerp(280f, 520f,
                Hash01(anchorSeed, soulIndex * 11 + 4));
            Vector2 start = center + angle.ToRotationVector2() * radius;
            Vector2 target = center
                + bladeAxis * MathHelper.Lerp(-76f, 78f,
                    Hash01(anchorSeed, soulIndex * 11 + 5)) * scale
                + bladeNormal * MathHelper.Lerp(-9f, 9f,
                    Hash01(anchorSeed, soulIndex * 11 + 6)) * scale;
            Vector2 chord = target - start;
            float side = Hash01(anchorSeed, soulIndex * 11 + 7) > 0.5f ? 1f : -1f;
            Vector2 control = (start + target) * 0.5f
                + SafeNormalize(chord, Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                * MathHelper.Lerp(80f, 170f, Hash01(anchorSeed, soulIndex * 11 + 8)) * side;

            float curveTime = Smooth01(travel);
            position = QuadraticBezier(start, control, target, curveTime);
            Vector2 tangent = QuadraticTangent(start, control, target, curveTime);
            direction = SafeNormalize(tangent, SafeNormalize(chord, Vector2.UnitX));
            soulScale = MathHelper.Lerp(0.68f, 1.12f,
                Hash01(anchorSeed, soulIndex * 11 + 9));
            soulScale *= MathHelper.Lerp(1f, 0.72f, curveTime);
            return true;
        }

        private static void ConfigureMaterializeEffect(Effect effect, Texture2D sword,
            Vector2 worldCenter, float rotation, float scale, float progress, float groundY) {

            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uTextureSize"]?.SetValue(new Vector2(sword.Width, sword.Height));
            effect.Parameters["uScale"]?.SetValue(scale);
            effect.Parameters["uRotation"]?.SetValue(rotation);
            effect.Parameters["uCenterY"]?.SetValue(worldCenter.Y);
            effect.Parameters["uGroundY"]?.SetValue(groundY);
            effect.Parameters["uSoulColor"]?.SetValue(SoulMain.ToVector3());
            effect.Parameters["uEdgeColor"]?.SetValue(SoulEdge.ToVector3());
        }

        private static void FillFilament(Vector2 start, Vector2 control, Vector2 target,
            float tail, float head, float scale) {

            for (int sample = 0; sample < FilamentSamples; sample++) {
                float factor = sample / (float)(FilamentSamples - 1);
                float curveTime = MathHelper.Lerp(tail, head, factor);
                Vector2 position = QuadraticBezier(start, control, target, curveTime);
                Vector2 tangent = QuadraticTangent(start, control, target, curveTime);
                Vector2 normal = SafeNormalize(tangent, Vector2.UnitX)
                    .RotatedBy(MathHelper.PiOver2);
                float width = MathHelper.Lerp(8.5f, 2.2f, factor) * MathHelper.Clamp(scale, 0.65f, 1.35f);
                Color color = Color.White;
                FilamentVertices[sample * 2] = new VertexPositionColorTexture(
                    (position + normal * width).ToVector3(), color, new Vector2(factor, 0f));
                FilamentVertices[sample * 2 + 1] = new VertexPositionColorTexture(
                    (position - normal * width).ToVector3(), color, new Vector2(factor, 1f));
            }
        }

        private static void AddWraithQuad(VertexPositionColorTexture[] vertices,
            ref int index, Vector2 center, Vector2 forward, float halfWidth,
            float halfHeight, Color color) {

            Vector2 perpendicular = forward.RotatedBy(MathHelper.PiOver2);
            Vector2 topLeft = center - forward * halfWidth - perpendicular * halfHeight;
            Vector2 topRight = center + forward * halfWidth - perpendicular * halfHeight;
            Vector2 bottomRight = center + forward * halfWidth + perpendicular * halfHeight;
            Vector2 bottomLeft = center - forward * halfWidth + perpendicular * halfHeight;

            vertices[index++] = new VertexPositionColorTexture(topLeft.ToVector3(), color, new Vector2(0f, 0f));
            vertices[index++] = new VertexPositionColorTexture(topRight.ToVector3(), color, new Vector2(1f, 0f));
            vertices[index++] = new VertexPositionColorTexture(bottomRight.ToVector3(), color, new Vector2(1f, 1f));
            vertices[index++] = new VertexPositionColorTexture(topLeft.ToVector3(), color, new Vector2(0f, 0f));
            vertices[index++] = new VertexPositionColorTexture(bottomRight.ToVector3(), color, new Vector2(1f, 1f));
            vertices[index++] = new VertexPositionColorTexture(bottomLeft.ToVector3(), color, new Vector2(0f, 1f));
        }

        private static void SetQuad(VertexPositionColorTexture[] vertices, Vector2 center,
            Vector2 right, Vector2 down, float halfWidth, float halfHeight, Color color) {

            vertices[0] = new VertexPositionColorTexture(
                (center - right * halfWidth - down * halfHeight).ToVector3(), color, new Vector2(0f, 0f));
            vertices[1] = new VertexPositionColorTexture(
                (center + right * halfWidth - down * halfHeight).ToVector3(), color, new Vector2(1f, 0f));
            vertices[2] = new VertexPositionColorTexture(
                (center - right * halfWidth + down * halfHeight).ToVector3(), color, new Vector2(0f, 1f));
            vertices[3] = new VertexPositionColorTexture(
                (center + right * halfWidth + down * halfHeight).ToVector3(), color, new Vector2(1f, 1f));
        }

        private static void SpawnImpactBurst(Vector2 impactCenter) {
            Color groundColor = GetLighting(impactCenter);
            for (int i = 0; i < 26; i++) {
                float horizontal = Main.rand.NextFloat(-6.5f, 6.5f);
                float vertical = -Main.rand.NextFloat(1.2f, 8.2f);
                Color color = Color.Lerp(SoulDark, SoulEdge, Main.rand.NextFloat(0.15f, 0.82f));
                PRTLoader.NewParticle<PRT_Spark>(
                    impactCenter + Main.rand.NextVector2Circular(12f, 5f),
                    new Vector2(horizontal, vertical), color,
                    Main.rand.NextFloat(0.65f, 1.45f)).Configure(i < 10, Main.rand.Next(22, 39));
            }

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.Lerp(MathHelper.Pi, MathHelper.TwoPi,
                    Main.rand.NextFloat());
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 7.5f);
                PRTLoader.NewParticle<PRT_Spark>(impactCenter, velocity,
                    Color.Lerp(SoulMain, SoulEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.55f, 1.15f)).Configure(false, Main.rand.Next(18, 30));
            }

            for (int i = 0; i < 14; i++) {
                Vector2 velocity = new(Main.rand.NextFloat(-3.4f, 3.4f),
                    -Main.rand.NextFloat(0.5f, 4.6f));
                Dust dust = Dust.NewDustPerfect(impactCenter + Main.rand.NextVector2Circular(15f, 4f),
                    DustID.Stone, velocity, 90, groundColor, Main.rand.NextFloat(0.75f, 1.25f));
                dust.noGravity = false;
            }
        }

        private static void DrawFallback(SpriteBatch spriteBatch, Texture2D sword,
            Vector2 worldCenter, float rotation, float scale, float progress, float opacity) {

            float reveal = SmoothStep(0.08f, 0.82f, progress);
            if (reveal <= 0.001f) {
                return;
            }

            spriteBatch.Draw(sword, worldCenter - Main.screenPosition, null,
                GetLighting(worldCenter) * (opacity * reveal), rotation,
                sword.Size() * 0.5f, scale, SpriteEffects.None, 0f);
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
                $"Weaver Grievances materialization renderer fallback: {exception.Message}");
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
