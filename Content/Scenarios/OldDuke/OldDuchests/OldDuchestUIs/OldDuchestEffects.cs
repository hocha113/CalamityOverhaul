using CalamityOverhaul.Content.UIs.StorageUIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests.OldDuchestUIs
{
    /// <summary>老箱子UI特效</summary>
    internal class OldDuchestEffects : IChestEffects
    {
        private readonly List<DustParticle> dustParticles = new();
        private readonly List<GlowMote> glowMotes = new();
        private int dustSpawnTimer = 0;
        private int glowSpawnTimer = 0;

        public void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            UpdateDustParticles(isActive, panelPosition, panelWidth, panelHeight);
            UpdateGlowMotes(isActive, panelPosition, panelWidth, panelHeight);
        }

        private void UpdateDustParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            for (int i = dustParticles.Count - 1; i >= 0; i--) {
                dustParticles[i].Update();
                if (dustParticles[i].ShouldRemove()) {
                    dustParticles.RemoveAt(i);
                }
            }

            dustSpawnTimer++;
            if (isActive && dustSpawnTimer >= 10 && dustParticles.Count < 20) {
                dustSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(30, panelWidth - 30),
                    Main.rand.NextFloat(70, panelHeight - 40)
                );
                dustParticles.Add(new DustParticle(spawnPos));
            }
        }

        private void UpdateGlowMotes(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            for (int i = glowMotes.Count - 1; i >= 0; i--) {
                glowMotes[i].Update();
                if (glowMotes[i].ShouldRemove()) {
                    glowMotes.RemoveAt(i);
                }
            }

            glowSpawnTimer++;
            if (isActive && glowSpawnTimer >= 22 && glowMotes.Count < 12) {
                glowSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(40, panelWidth - 40),
                    Main.rand.NextFloat(90, panelHeight - 50)
                );
                glowMotes.Add(new GlowMote(spawnPos));
            }
        }

        public void DrawEffects(SpriteBatch spriteBatch, float uiAlpha) {
            //暖光在底层
            foreach (var mote in glowMotes) {
                mote.Draw(spriteBatch, uiAlpha);
            }
            //灰尘在上层
            foreach (var dust in dustParticles) {
                dust.Draw(spriteBatch, uiAlpha);
            }
        }

        public void Clear() {
            dustParticles.Clear();
            glowMotes.Clear();
            dustSpawnTimer = 0;
            glowSpawnTimer = 0;
        }

        //木质灰尘粒子，带色调变化和横向飘动
        private class DustParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Alpha;
            public int Life;
            private readonly int maxLife;
            private readonly Color tint;

            public DustParticle(Vector2 position) {
                Position = position;
                Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.9f, -0.15f));
                Scale = Main.rand.NextFloat(0.3f, 0.7f);
                Alpha = 1f;
                Life = maxLife = Main.rand.Next(50, 110);
                int r = 120 + Main.rand.Next(40);
                int g = 70 + Main.rand.Next(30);
                int b = 30 + Main.rand.Next(20);
                tint = new Color(r, g, b);
            }

            public void Update() {
                Position += Velocity;
                Velocity.Y -= 0.015f;
                Velocity.X += (float)Math.Sin(Life * 0.08f) * 0.01f;
                Velocity.X *= 0.97f;
                Alpha = Life / (float)maxLife;
                Scale *= 0.994f;
                Life--;
            }

            public bool ShouldRemove() => Life <= 0;

            public void Draw(SpriteBatch spriteBatch, float uiAlpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Color drawColor = tint * (Alpha * uiAlpha * 0.35f);
                spriteBatch.Draw(pixel, Position, null, drawColor,
                    0f, Vector2.One * 0.5f, Scale * 3f, SpriteEffects.None, 0f);
            }
        }

        //暖光粒子，缓慢上浮并脉冲闪烁
        private class GlowMote
        {
            public Vector2 Position;
            private readonly Vector2 origin;
            public float Scale;
            public int Life;
            private readonly int maxLife;
            private readonly float phaseOffset;
            private readonly float driftSpeed;

            public GlowMote(Vector2 position) {
                Position = position;
                origin = position;
                Scale = Main.rand.NextFloat(2f, 5f);
                Life = maxLife = Main.rand.Next(80, 180);
                phaseOffset = Main.rand.NextFloat(MathHelper.TwoPi);
                driftSpeed = Main.rand.NextFloat(0.15f, 0.35f);
            }

            public void Update() {
                float progress = 1f - Life / (float)maxLife;
                Position = origin + new Vector2(
                    (float)Math.Sin(phaseOffset + progress * 4f) * 6f,
                    -progress * 30f * driftSpeed
                );
                Life--;
            }

            public bool ShouldRemove() => Life <= 0;

            public void Draw(SpriteBatch spriteBatch, float uiAlpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float progress = 1f - Life / (float)maxLife;
                float alpha = (float)Math.Sin(progress * MathHelper.Pi) * 0.6f;
                float pulse = 1f + (float)Math.Sin(phaseOffset + progress * 8f) * 0.2f;
                Color glowColor = new Color(220, 160, 60) * (alpha * uiAlpha * 0.25f);
                spriteBatch.Draw(pixel, Position, null, glowColor,
                    0f, Vector2.One * 0.5f, Scale * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
