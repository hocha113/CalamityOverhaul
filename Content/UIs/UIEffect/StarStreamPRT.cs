using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>星流粒子，模拟流动的金色星尘光带</summary>
    public class StarStreamPRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float Size = Main.rand.NextFloat(1.8f, 4f);
        public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(90f, 160f);
        public float Seed = Main.rand.NextFloat(10f);
        public Vector2 Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.5f, -0.15f));
        public float Brightness = Main.rand.NextFloat(0.6f, 1f);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Rot += 0.018f;
            Pos += Velocity;
            Velocity.Y -= 0.008f;
            Velocity.X += (float)Math.Sin(Life * 0.05f + Seed) * 0.02f;

            if (Life >= MaxLife) return true;
            if (Pos.X < panelPos.X - 50 || Pos.X > panelPos.X + panelSize.X + 50 ||
                Pos.Y < panelPos.Y - 50 || Pos.Y > panelPos.Y + panelSize.Y + 50) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha * Brightness;
            float scale = Size * (0.6f + (float)Math.Sin((Life + Seed * 30f) * 0.07f) * 0.4f);

            Texture2D px = VaultAsset.placeholder2.Value;

            //金色核心
            Color gold = new Color(255, 210, 100) * (0.85f * fade);
            sb.Draw(px, Pos, null, gold, Rot, new Vector2(0.5f),
                new Vector2(scale * 2.5f, scale * 0.35f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, gold * 0.8f, Rot + MathHelper.PiOver2, new Vector2(0.5f),
                new Vector2(scale * 2.5f, scale * 0.35f), SpriteEffects.None, 0f);

            //暖白光晕
            Color warm = new Color(255, 240, 200) * (0.4f * fade);
            sb.Draw(px, Pos, null, warm, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.6f), SpriteEffects.None, 0f);
        }
    }
}
