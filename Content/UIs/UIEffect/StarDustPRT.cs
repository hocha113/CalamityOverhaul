using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>星尘节点</summary>
    public class StarDustPRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public float Radius = Main.rand.NextFloat(2f, 4.5f);
        public float PulseSpeed = Main.rand.NextFloat(0.6f, 1.4f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(110f, 200f);
        public float Seed = Main.rand.NextFloat(10f);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Pos.X += (float)Math.Sin(Life * 0.03f + Seed * 3f) * 0.15f;
            Pos.Y -= 0.08f;
            return Life >= MaxLife;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi);
            float pulse = (float)Math.Sin((Life + Seed * 20f) * 0.06f * PulseSpeed) * 0.5f + 0.5f;
            float scale = Radius * (0.7f + pulse * 0.5f);

            Color core = new Color(255, 220, 120) * (alpha * 0.65f * fade);
            Color ring = new Color(200, 160, 60) * (alpha * 0.35f * fade);

            sb.Draw(px, Pos, null, ring, 0f, new Vector2(0.5f),
                new Vector2(scale * 2.4f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, core, 0f, new Vector2(0.5f),
                new Vector2(scale), SpriteEffects.None, 0f);

            Color bright = new Color(255, 250, 230) * (alpha * 0.45f * fade * pulse);
            sb.Draw(px, Pos, null, bright, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.35f), SpriteEffects.None, 0f);
        }
    }
}
