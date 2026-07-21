using CalamityOverhaul.Content.Industrials.Generator.Hydroelectrics;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses
{
    /// <summary>海洋吞噬者工作VFX</summary>
    internal class OceanRaidersVortexEffect
    {
        private readonly OceanRaidersTP machine;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Cyclone { get; set; }

        private float vortexRotation = 0f;
        private float vortexIntensity = 0f;
        private float vortexPulse = 0f;

        private readonly List<PRT_HomeBubble> risingBubbles = new();
        private int bubbleSpawnTimer = 0;

        private readonly List<WaterStreamParticle> waterStreams = new();
        private int streamSpawnTimer = 0;

        public OceanRaidersVortexEffect(OceanRaidersTP machine) {
            this.machine = machine;
        }

        public void Update() {
            if (!machine.isWorking) {
                vortexIntensity = Math.Max(0, vortexIntensity - 0.02f);
                ClearParticles();
                return;
            }

            if (vortexIntensity < 1f) {
                vortexIntensity += 0.03f;
            }

            vortexRotation += 0.08f + vortexIntensity * 0.12f;
            if (vortexRotation > MathHelper.TwoPi) {
                vortexRotation -= MathHelper.TwoPi;
            }

            vortexPulse += 0.05f;
            if (vortexPulse > MathHelper.TwoPi) {
                vortexPulse -= MathHelper.TwoPi;
            }

            UpdateBubbles();

            UpdateWaterStreams();
        }

        private void UpdateBubbles() {
            if (VaultUtils.isServer) return;

            for (int i = risingBubbles.Count - 1; i >= 0; i--) {
                if (!risingBubbles[i].active) {
                    risingBubbles.RemoveAt(i);
                }
            }

            bubbleSpawnTimer++;
            if (bubbleSpawnTimer >= Math.Max(4, 10 - (int)(vortexIntensity * 6))) {
                bubbleSpawnTimer = 0;
                int spawnCount = Main.rand.Next(2, 4);

                for (int i = 0; i < spawnCount; i++) {
                    SpawnBubble();
                }
            }
        }

        private void SpawnBubble() {
            Vector2 waterSurfacePos = FindWaterSurface();
            if (waterSurfacePos == Vector2.Zero) return;

            Vector2 spawnPos = waterSurfacePos + new Vector2(
                Main.rand.NextFloat(-60f, 60f),
                Main.rand.NextFloat(20f, 100f)
            );

            //须在水中
            Tile tile = Framing.GetTileSafely(spawnPos);
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                Vector2 targetPos = machine.intakeCenter;
                BasePRT prt = PRTLoader.NewParticle<PRT_HomeBubble>(
                    spawnPos,
                    new Vector2(0, -4),
                    Color.White,
                    Main.rand.NextFloat(0.3f, 0.8f)
                );

                prt.ai[0] = targetPos.X;
                prt.ai[1] = targetPos.Y;

                if (prt is PRT_HomeBubble bubble) {
                    risingBubbles.Add(bubble);
                }
            }
        }

        private void UpdateWaterStreams() {
            if (VaultUtils.isServer) return;

            for (int i = waterStreams.Count - 1; i >= 0; i--) {
                waterStreams[i].Update();
                if (waterStreams[i].ShouldRemove()) {
                    waterStreams.RemoveAt(i);
                }
            }

            streamSpawnTimer++;
            if (streamSpawnTimer >= 2) {
                streamSpawnTimer = 0;
                SpawnWaterStream();
            }
        }

        private void SpawnWaterStream() {
            Vector2 waterSurfacePos = FindWaterSurface();
            if (waterSurfacePos == Vector2.Zero) return;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(30f, 80f);
            Vector2 offset = angle.ToRotationVector2() * radius;

            Vector2 spawnPos = waterSurfacePos + offset + new Vector2(0, Main.rand.NextFloat(0, 60f));

            //须在水中
            Tile tile = Framing.GetTileSafely(spawnPos);
            if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                waterStreams.Add(new WaterStreamParticle(
                    spawnPos,
                    machine.intakeCenter,
                    vortexRotation
                ));
            }
        }

        private Vector2 FindWaterSurface() {
            Point startPoint = (machine.Position + new Point16(3, 6)).ToPoint();

            for (int y = 0; y < 32; y++) {
                for (int x = -2; x <= 2; x++) {
                    Tile tile = Framing.GetTileSafely(startPoint.X + x, startPoint.Y + y);
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water) {
                        return new Vector2(
                            (startPoint.X + x) * 16 + 8,
                            (startPoint.Y + y) * 16 + 8
                        );
                    }
                }
            }

            return Vector2.Zero;
        }

        public void DrawVortex(SpriteBatch spriteBatch) {
            if (vortexIntensity <= 0.01f || VaultUtils.isServer) return;
            if (Cyclone == null || Cyclone.IsDisposed) return;

            Vector2 waterSurfacePos = FindWaterSurface();
            if (waterSurfacePos == Vector2.Zero) return;

            Texture2D vortexTexture = Cyclone.Value;
            Vector2 intakePos = machine.intakeCenter;

            float vortexLength = Vector2.Distance(intakePos, waterSurfacePos);
            if (vortexLength < 10f) return;

            int layerCount = 4;
            for (int layer = 0; layer < layerCount; layer++) {
                float layerProgress = layer / (float)layerCount;
                float rotationOffset = vortexRotation + layerProgress * MathHelper.TwoPi;

                //上小下大
                float topScale = 0.3f + (float)Math.Sin(vortexPulse + layerProgress) * 0.1f;
                float bottomScale = 1.2f + (float)Math.Sin(vortexPulse + layerProgress + MathHelper.Pi) * 0.2f;

                int segmentCount = 92;
                for (int i = 0; i < segmentCount; i++) {
                    float t = i / (float)segmentCount;
                    Vector2 segmentPos = Vector2.Lerp(intakePos, waterSurfacePos, t);

                    float spiralOffset = (float)Math.Sin(vortexRotation * 2 + t * MathHelper.TwoPi * 2 + layerProgress) * 15f * vortexIntensity;
                    segmentPos.X += spiralOffset;

                    float segmentScale = MathHelper.Lerp(topScale, bottomScale, t) * vortexIntensity;

                    //上透下浓
                    float alpha = MathHelper.Lerp(0.15f, 0.4f, t) * vortexIntensity;
                    Color drawColor = Color.Lerp(
                        new Color(100, 200, 255),
                        new Color(80, 180, 240),
                        layerProgress
                    ) * alpha;

                    drawColor.A = 0;

                    Vector2 drawPos = segmentPos - Main.screenPosition;
                    spriteBatch.Draw(
                        vortexTexture,
                        drawPos,
                        null,
                        drawColor,
                        rotationOffset + t * MathHelper.Pi * 0.5f,
                        vortexTexture.Size() / 2f,
                        new Vector2(segmentScale * 0.8f, segmentScale * 0.3f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            DrawVortexGlow(spriteBatch, intakePos, waterSurfacePos, vortexLength);
        }

        private void DrawVortexGlow(SpriteBatch spriteBatch, Vector2 topPos, Vector2 bottomPos, float length) {
            Texture2D vortexTexture = Cyclone.Value;
            float glowIntensity = (float)Math.Sin(vortexPulse) * 0.3f + 0.5f;

            Vector2 topDrawPos = topPos - Main.screenPosition;
            Color topGlow = new Color(150, 220, 255, 0) * (glowIntensity * vortexIntensity * 0.6f);
            spriteBatch.Draw(
                vortexTexture,
                topDrawPos,
                null,
                topGlow,
                vortexRotation,
                vortexTexture.Size() / 2f,
                new Vector2(0.5f, 0.5f) * vortexIntensity,
                SpriteEffects.None,
                0f
            );

            Vector2 bottomDrawPos = bottomPos - Main.screenPosition;
            Color bottomGlow = new Color(100, 200, 255, 0) * (glowIntensity * vortexIntensity * 0.8f);
            spriteBatch.Draw(
                vortexTexture,
                bottomDrawPos,
                null,
                bottomGlow,
                -vortexRotation,
                vortexTexture.Size() / 2f,
                new Vector2(1.5f, 1.5f) * vortexIntensity,
                SpriteEffects.None,
                0f
            );
        }

        private void ClearParticles() {
            risingBubbles.Clear();
            waterStreams.Clear();
        }
    }

    internal class WaterStreamParticle
    {
        public Vector2 Position;
        public Vector2 TargetPosition;
        public float Rotation;
        public float Scale;
        public float Alpha;
        public int Life;
        private readonly int maxLife;

        public WaterStreamParticle(Vector2 position, Vector2 target, float baseRotation) {
            Position = position;
            TargetPosition = target;
            Rotation = baseRotation + Main.rand.NextFloat(-0.5f, 0.5f);
            Scale = Main.rand.NextFloat(0.4f, 0.8f);
            Alpha = 1f;
            Life = maxLife = Main.rand.Next(30, 60);
        }

        public void Update() {
            Vector2 direction = (TargetPosition - Position).SafeNormalize(Vector2.Zero);
            float speed = 3f + (1f - Life / (float)maxLife) * 5f;
            Position += direction * speed;

            float spiralAngle = Life * 0.15f;
            Vector2 spiralOffset = spiralAngle.ToRotationVector2() * 8f;
            Position += spiralOffset;

            Alpha = Life / (float)maxLife;
            Scale *= 0.98f;

            Life--;
        }

        public bool ShouldRemove() => Life <= 0 || Vector2.Distance(Position, TargetPosition) < 20f;

        public void Draw(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;
            Color drawColor = new Color(120, 200, 255) * (Alpha * 0.6f);

            spriteBatch.Draw(
                pixel,
                drawPos,
                null,
                drawColor,
                Rotation,
                Vector2.One * 0.5f,
                new Vector2(Scale * 2f, Scale * 8f),
                SpriteEffects.None,
                0f
            );
        }
    }
}
