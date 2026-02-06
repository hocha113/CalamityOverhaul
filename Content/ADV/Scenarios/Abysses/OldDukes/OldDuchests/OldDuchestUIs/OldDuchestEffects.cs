using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI视觉特效管理器
    /// </summary>
    internal class OldDuchestEffects
    {
        //粒子列表
        private readonly List<DustParticle> dustParticles = new();
        private int dustSpawnTimer = 0;

        /// <summary>
        /// 更新所有粒子和特效
        /// </summary>
        public void UpdateParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            UpdateDustParticles(isActive, panelPosition, panelWidth, panelHeight);
        }

        private void UpdateDustParticles(bool isActive, Vector2 panelPosition, int panelWidth, int panelHeight) {
            //更新现有粒子
            for (int i = dustParticles.Count - 1; i >= 0; i--) {
                dustParticles[i].Update();
                if (dustParticles[i].ShouldRemove()) {
                    dustParticles.RemoveAt(i);
                }
            }

            //生成新粒子
            dustSpawnTimer++;
            if (isActive && dustSpawnTimer >= 20 && dustParticles.Count < 10) {
                dustSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(50, panelWidth - 50),
                    Main.rand.NextFloat(80, panelHeight - 50)
                );
                dustParticles.Add(new DustParticle(spawnPos));
            }
        }

        /// <summary>
        /// 绘制所有特效
        /// </summary>
        public void DrawEffects(SpriteBatch spriteBatch, float uiAlpha) {
            foreach (var dust in dustParticles) {
                dust.Draw(spriteBatch, uiAlpha);
            }
        }

        /// <summary>
        /// 清空所有特效
        /// </summary>
        public void Clear() {
            dustParticles.Clear();
            dustSpawnTimer = 0;
        }

        //简单的灰尘粒子
        private class DustParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Alpha;
            public int Life;
            private readonly int maxLife;

            public DustParticle(Vector2 position) {
                Position = position;
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.2f));
                Scale = Main.rand.NextFloat(0.4f, 0.8f);
                Alpha = 1f;
                Life = maxLife = Main.rand.Next(60, 120);
            }

            public void Update() {
                Position += Velocity;
                Velocity.Y -= 0.02f;
                Velocity.X *= 0.98f;
                Alpha = Life / (float)maxLife;
                Scale *= 0.995f;
                Life--;
            }

            public bool ShouldRemove() => Life <= 0;

            public void Draw(SpriteBatch spriteBatch, float uiAlpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Color drawColor = new Color(139, 87, 42) * (Alpha * uiAlpha * 0.3f);
                spriteBatch.Draw(pixel, Position - Main.screenPosition, null, drawColor,
                    0f, Vector2.One * 0.5f, Scale * 3f, SpriteEffects.None, 0f);
            }
        }
    }
}
