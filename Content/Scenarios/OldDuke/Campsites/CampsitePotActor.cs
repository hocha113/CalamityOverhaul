using CalamityOverhaul.Content.Industrials.Generator.Hydroelectrics;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>
    /// 营地里的锅，自带发光/沸腾/水下判定与蒸汽气泡特效
    /// </summary>
    internal class CampsitePotActor : Actor
    {
        /// <summary>
        /// 是否正被老公爵访问，由 <see cref="OldDukeWanderingActor"/> 在服务端/单人下写入，标记同步以便客户端也能表现访问反馈
        /// </summary>
        [SyncVar]
        public bool IsBeingVisited;
        /// <summary>
        /// 交互强度，由 <see cref="OldDukeWanderingActor"/> 在服务端/单人下写入
        /// </summary>
        [SyncVar]
        public float InteractionIntensity;

        private float glowTimer;
        private float bouncePhase;
        private bool isUnderwater;
        private int steamSpawnTimer;
        private int bubbleSpawnTimer;
        private int waterBubbleSpawnTimer;

        public override void OnSpawn(params object[] args) {
            Width = 46;
            Height = 48;
            DrawExtendMode = 200;
            DrawLayer = ActorDrawLayer.AfterTiles;

            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
            bouncePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            IsBeingVisited = false;
            InteractionIntensity = 0f;
        }

        public override void AI() {
            glowTimer += 0.025f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            isUnderwater = CheckUnderwater();

            if (IsBeingVisited) {
                bouncePhase += 0.2f * (0.5f + InteractionIntensity * 1.5f);
            }
            else {
                bouncePhase += 0.05f;
                //无人来访时交互强度自然回落；来访时的拉升由 OldDukeWanderingActor 负责
                InteractionIntensity = MathHelper.Lerp(InteractionIntensity, 0f, 0.05f);
            }
            if (bouncePhase > MathHelper.TwoPi) {
                bouncePhase -= MathHelper.TwoPi;
            }

            if (!Main.dedServ) {
                Lighting.AddLight(Position, TorchID.Yellow);
                UpdateParticles();
            }
        }

        private bool CheckUnderwater() {
            Point tileCoord = (Position / 16).ToPoint();
            for (int y = -2; y <= 0; y++) {
                Tile tile = Framing.GetTileSafely(tileCoord.X, tileCoord.Y + y);
                if (tile.LiquidAmount > 128 && tile.LiquidType == LiquidID.Water) {
                    return true;
                }
            }
            return false;
        }

        private void UpdateParticles() {
            if (isUnderwater) {
                waterBubbleSpawnTimer++;
                int bubbleRate = IsBeingVisited ? 4 : 8;
                if (waterBubbleSpawnTimer >= bubbleRate) {
                    waterBubbleSpawnTimer = 0;
                    SpawnWaterBubble();
                    if (IsBeingVisited && InteractionIntensity > 0.5f && Main.rand.NextBool(2)) {
                        SpawnWaterBubble();
                    }
                }
                return;
            }

            steamSpawnTimer++;
            int baseSpawnRate = IsBeingVisited ? 6 : 10;
            if (steamSpawnTimer >= baseSpawnRate) {
                steamSpawnTimer = 0;
                SpawnSteamParticle(false);
                if (IsBeingVisited && InteractionIntensity > 0.5f && Main.rand.NextBool(2)) {
                    SpawnSteamParticle(true);
                }
            }

            bubbleSpawnTimer++;
            int bubbleSpawnRate = IsBeingVisited ? 8 : 15;
            if (bubbleSpawnTimer >= bubbleSpawnRate) {
                bubbleSpawnTimer = 0;
                SpawnBoilBubble();
            }
        }

        private void SpawnSteamParticle(bool isEnhanced) {
            Vector2 spawnPos = Position + new Vector2(Main.rand.NextFloat(-12f, 12f), -24f + Main.rand.NextFloat(-4f, 4f));
            Vector2 velocity = isEnhanced
                ? new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-2.5f, -1.5f))
                : new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.5f, -0.8f));
            float scale = isEnhanced ? Main.rand.NextFloat(0.7f, 1.3f) : Main.rand.NextFloat(0.4f, 0.8f);
            int life = isEnhanced ? Main.rand.Next(35, 55) : Main.rand.Next(45, 75);

            PRTLoader.NewParticle<PRT_CampfireSteam>(spawnPos, velocity, Color.White, scale).Configure(life, isEnhanced);
        }

        private void SpawnBoilBubble() {
            Vector2 spawnPos = Position + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-8f, 0f));
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.8f, -0.4f));
            float scale = Main.rand.NextFloat(0.1f, 0.2f);

            PRTLoader.NewParticle<PRT_CampfireBubble>(spawnPos, velocity, Color.White, scale).Configure(Main.rand.Next(20, 35));
        }

        private void SpawnWaterBubble() {
            Vector2 spawnPos = Position + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-8f, 8f));
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-2f, -1f));
            float scale = Main.rand.NextFloat(0.3f, 0.6f);
            if (IsBeingVisited && InteractionIntensity > 0.5f) {
                scale *= 1.3f;
            }

            PRTLoader.NewParticle<PRT_WaterBubble>(spawnPos, velocity, Color.White, scale);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (OldDukeCampsite.OldPot == null) {
                return false;
            }

            Texture2D potTexture = OldDukeCampsite.OldPot;
            Vector2 screenPos = Position - Main.screenPosition;
            Vector2 origin = potTexture.Size() / 2f;

            //跳动效果偏移
            float bounceOffset = 0f;
            if (IsBeingVisited && InteractionIntensity > 0.3f) {
                bounceOffset = MathF.Sin(bouncePhase * 2f) * 4f * InteractionIntensity;
            }
            Vector2 bounceVector = new Vector2(0, bounceOffset);

            //基础发光强度
            float baseGlowIntensity = (MathF.Sin(glowTimer * 3f) * 0.5f + 0.5f) * 0.6f;
            float glowIntensity = baseGlowIntensity * (1f + InteractionIntensity * 1.8f);
            Color fireGlow = new Color(255, 120, 60) with { A = 0 };

            //交互时的额外光晕层
            if (IsBeingVisited && InteractionIntensity > 0.2f) {
                for (int i = 0; i < 2; i++) {
                    float extraGlowScale = 1.4f + i * 0.15f;
                    float extraGlowAlpha = InteractionIntensity * 0.3f * (1f - i * 0.4f);

                    spriteBatch.Draw(potTexture, screenPos + bounceVector, null,
                        new Color(255, 180, 100) with { A = 0 } * extraGlowAlpha, 0f, origin, extraGlowScale, SpriteEffects.None, 0f);
                }
            }

            //基础发光层
            for (int i = 0; i < 3; i++) {
                float glowScale = 1.1f + i * 0.08f;
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                Vector2 glowOffset = new Vector2(0, -6f + i * 2f);

                spriteBatch.Draw(potTexture, screenPos + bounceVector + glowOffset, null,
                    fireGlow * glowAlpha, 0f, origin, glowScale, SpriteEffects.None, 0f);
            }

            //锅主体轻微摇晃效果
            float potRotation = 0f;
            if (IsBeingVisited && InteractionIntensity > 0.4f) {
                potRotation = MathF.Sin(bouncePhase * 3f) * 0.15f * InteractionIntensity;
            }

            spriteBatch.Draw(potTexture, screenPos + bounceVector, null,
                Lighting.GetColor((Position / 16).ToPoint()), potRotation, origin, 1f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
