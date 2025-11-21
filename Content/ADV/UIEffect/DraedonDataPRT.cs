using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class DraedonDataPRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float Size = Main.rand.NextFloat(1.5f, 3.5f);
        public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(80f, 150f);
        public float Seed = Main.rand.NextFloat(10f);
        public Vector2 Velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.6f, -0.2f));

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Rot += 0.025f;
            Pos += Velocity;
            Velocity.Y -= 0.015f;

            if (Life >= MaxLife) return true;
            if (Pos.X < panelPos.X - 50 || Pos.X > panelPos.X + panelSize.X + 50 ||
                Pos.Y < panelPos.Y - 50 || Pos.Y > panelPos.Y + panelSize.Y + 50) {
                return true;
            }
            return false;
        }

        public bool Update(Vector2 basePos) {
            Life++;
            Rot += 0.025f;
            Pos += Velocity;
            Velocity.Y -= 0.015f;

            if (Life >= MaxLife) {
                return true;
            }

            //边界检查
            if (Pos.X < basePos.X - 150f || Pos.X > basePos.X + 150f ||
                Pos.Y < basePos.Y - 100f || Pos.Y > basePos.Y + 100f) {
                return true;
            }

            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
            float scale = Size * (0.7f + (float)Math.Sin((Life + Seed * 40f) * 0.09f) * 0.3f);

            Color c = new Color(80, 200, 255) * (0.8f * fade);
            Texture2D px = VaultAsset.placeholder2.Value;

            sb.Draw(px, Pos, null, c, Rot, new Vector2(0.5f),
                new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, c * 0.9f, Rot + MathHelper.PiOver2, new Vector2(0.5f),
                new Vector2(scale * 2f, scale * 0.3f), SpriteEffects.None, 0f);
        }
    }
}
