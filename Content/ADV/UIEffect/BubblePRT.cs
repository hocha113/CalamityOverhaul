using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// º£ÑóÆøÅÝÁ£×Ó
    /// </summary>
    public class BubblePRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float Radius = Main.rand.NextFloat(3f, 7f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(60f, 120f);
        public float RiseSpeed = Main.rand.NextFloat(0.55f, 1.25f);
        public float Drift = Main.rand.NextFloat(-0.18f, 0.18f);
        public float Seed = Main.rand.NextFloat(10f);
        public Color CoreColor = new Color(140, 230, 255);
        public Color RimColor = new Color(30, 100, 150);
        public bool Update() {
            Life++;
            Pos.Y -= 0.6f + (float)Math.Sin(Life * 0.05f + Seed) * 0.2f;
            Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * 0.5f;
            return Life >= MaxLife;
        }

        public bool Update(Vector2 panelPos, Vector2 panelSize, float bubbleSideMargin = 34f) {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (0.85f + (float)Math.Sin(t * Math.PI) * 0.25f);
            Pos.X += (float)Math.Sin(Life * 0.045f + Seed) * Drift;

            float left = panelPos.X + bubbleSideMargin * 0.7f;
            float right = panelPos.X + panelSize.X - bubbleSideMargin * 0.7f;
            if (Pos.X < left) Pos.X = left;
            if (Pos.X > right) Pos.X = right;

            if (Life >= MaxLife || Pos.Y < panelPos.Y + 24f) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi);
            float scale = Radius * (0.8f + (float)Math.Sin((Life + Seed * 15f) * 0.08f) * 0.2f);
            Color core = CoreColor * (a * 0.4f * fade);
            Color rim = RimColor * (a * 0.25f * fade);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), rim, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale * 1.6f, scale * 0.5f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), core, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale), SpriteEffects.None, 0f);
        }

        public void DrawEnhanced(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * Math.PI);
            float scale = Radius * (0.9f + (float)Math.Sin((Life + Seed * 15f) * 0.1f) * 0.18f);
            Color core = CoreColor * (alpha * 0.55f * fade);
            Color rim = RimColor * (alpha * 0.4f * fade);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), rim, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale * 1.8f, scale * 0.55f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), core, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale), SpriteEffects.None, 0f);
        }
    }
}
