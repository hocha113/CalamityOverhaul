using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>数据流粒子，人体周围飘动</summary>
    internal class CyberDataParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public Color BaseColor;
        private readonly float phase;

        public CyberDataParticle(Vector2 pos, Vector2 vel, Color color) {
            Position = pos;
            Velocity = vel;
            BaseColor = color;
            MaxLife = 60 + Main.rand.Next(40);
            Life = MaxLife;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>粒子消亡返回 true</summary>
        public bool Update() {
            Life--;
            if (Life <= 0) return true;
            Position += Velocity;
            Position.X += MathF.Sin(phase + Life * 0.08f) * 0.15f;
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float progress = 1f - Life / MaxLife;
            float fadeAlpha = progress < 0.2f ? progress / 0.2f : progress > 0.7f ? (1f - progress) / 0.3f : 1f;
            float scale = 0.04f + MathF.Sin(progress * MathHelper.Pi) * 0.03f;
            Color drawColor = BaseColor * (alpha * fadeAlpha * 0.5f);
            drawColor.A = 0;
            sb.Draw(glow, Position, null, drawColor, 0, glow.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    /// <summary>数据流粒子系统</summary>
    internal class CyberDataParticleSystem
    {
        private readonly List<CyberDataParticle> particles = [];
        private int spawnTimer;

        /// <summary>在 origin 周围生成并更新粒子</summary>
        public void Update(Vector2 origin, float openProgress) {
            spawnTimer++;
            if (spawnTimer >= 6 && particles.Count < 50 && openProgress > 0.5f) {
                spawnTimer = 0;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = 20f + Main.rand.NextFloat(80f);
                Vector2 pos = origin + angle.ToRotationVector2() * dist;
                Vector2 vel = new(Main.rand.NextFloat(-0.3f, 0.3f), -0.5f - Main.rand.NextFloat(0.5f));
                Color c = Main.rand.NextBool(3)
                    ? CyberwareTheme.Accent
                    : Main.rand.NextBool() ? CyberwareTheme.AccentGold : CyberwareTheme.AccentCyan;
                particles.Add(new CyberDataParticle(pos, vel, c * 0.6f));
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) {
                    particles.RemoveAt(i);
                }
            }
        }

        /// <summary>绘制活跃粒子</summary>
        public void Draw(SpriteBatch sb, float alpha) {
            foreach (var p in particles) {
                p.Draw(sb, alpha);
            }
        }
    }
}
