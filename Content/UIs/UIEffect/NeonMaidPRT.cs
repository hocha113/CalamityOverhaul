using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.UIEffect
{
    /// <summary>霓虹菱形碎片</summary>
    public class NeonMaidPRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float Size = Main.rand.NextFloat(1.2f, 3f);
        public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(90f, 160f);
        public float Seed = Main.rand.NextFloat(10f);
        public Vector2 Velocity = new(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.45f, -0.12f));
        public float ColorLerp = Main.rand.NextFloat(1f);//蓝↔紫

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Rot += 0.018f;
            Pos += Velocity;
            Velocity.X += MathF.Sin(Life * 0.04f + Seed) * 0.003f;

            if (Life >= MaxLife) return true;
            if (Pos.X < panelPos.X - 40 || Pos.X > panelPos.X + panelSize.X + 40 ||
                Pos.Y < panelPos.Y - 40 || Pos.Y > panelPos.Y + panelSize.Y + 40) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = MathF.Sin(t * MathHelper.Pi) * alpha;
            float scale = Size * (0.75f + MathF.Sin((Life + Seed * 30f) * 0.07f) * 0.25f);

            Color neonBlue = new Color(60, 140, 255);
            Color neonViolet = new Color(150, 100, 240);
            Color c = Color.Lerp(neonBlue, neonViolet, ColorLerp) * (0.7f * fade);

            //菱形=十字叠
            sb.Draw(px, Pos, null, c, Rot, new Vector2(0.5f),
                new Vector2(scale * 1.8f, scale * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, c * 0.85f, Rot + MathHelper.PiOver2, new Vector2(0.5f),
                new Vector2(scale * 1.8f, scale * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, c * 0.5f, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.35f), SpriteEffects.None, 0f);
        }
    }
}
