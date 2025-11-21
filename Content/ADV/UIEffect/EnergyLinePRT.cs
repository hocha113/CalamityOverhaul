using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class EnergyLinePRT(Vector2 start, Vector2 end)
    {
        public Vector2 Start = start;
        public Vector2 End = end;
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(120f, 200f);
        public float FlowSpeed = Main.rand.NextFloat(0.02f, 0.05f);
        public float FlowOffset = Main.rand.NextFloat(MathHelper.TwoPi);

        public bool Update() {
            Life++;
            FlowOffset += FlowSpeed;
            if (FlowOffset > MathHelper.TwoPi) FlowOffset -= MathHelper.TwoPi;
            return Life >= MaxLife;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi);

            Vector2 direction = End - Start;
            float length = direction.Length();
            direction.Normalize();

            int segments = (int)(length / 15f);
            for (int i = 0; i < segments; i++) {
                float segT = i / (float)segments;
                float wave = (float)Math.Sin(FlowOffset + segT * MathHelper.TwoPi * 2f) * 0.5f + 0.5f;

                Vector2 pos = Start + direction * (length * segT);
                Color color = new Color(60, 160, 240) * (alpha * 0.15f * fade * wave);

                Texture2D px = VaultAsset.placeholder2.Value;
                sb.Draw(px, pos, null, color, 0f, new Vector2(0.5f),
                    new Vector2(3f, 1.5f), SpriteEffects.None, 0f);
            }
        }
    }
}
