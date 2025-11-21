using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class CircuitNodePRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public float Radius = Main.rand.NextFloat(2f, 5f);
        public float PulseSpeed = Main.rand.NextFloat(0.8f, 1.6f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(100f, 180f);
        public float Seed = Main.rand.NextFloat(10f);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            return Life >= MaxLife;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi);
            float pulse = (float)Math.Sin((Life + Seed * 20f) * 0.08f * PulseSpeed) * 0.5f + 0.5f;
            float scale = Radius * (0.8f + pulse * 0.4f);

            Color core = new Color(100, 220, 255) * (alpha * 0.7f * fade);
            Color ring = new Color(40, 140, 200) * (alpha * 0.5f * fade);

            sb.Draw(px, Pos, null, ring, 0f, new Vector2(0.5f),
                new Vector2(scale * 2.2f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, core, 0f, new Vector2(0.5f),
                new Vector2(scale), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, core * 0.4f, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.3f), SpriteEffects.None, 0f);
        }
    }
}
