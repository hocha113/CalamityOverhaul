using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class AshPRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public float Size = Main.rand.NextFloat(1.5f, 3.5f);
        public float RiseSpeed = Main.rand.NextFloat(0.15f, 0.45f);
        public float Drift = Main.rand.NextFloat(-0.35f, 0.35f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(100f, 180f);
        public float Seed = Main.rand.NextFloat(10f);
        public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (0.7f + (float)Math.Sin(t * Math.PI) * 0.3f);
            Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * Drift * 1.5f;

            if (Life >= MaxLife || Pos.Y < panelPos.Y) {
                return true;
            }
            return false;
        }

        public bool Update(Vector2 basePos) {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (0.7f + (float)Math.Sin(t * Math.PI) * 0.3f);
            Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * Drift * 1.5f;

            if (Life >= MaxLife || Pos.Y < basePos.Y - 100f) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * Math.PI) * (1f - t * 0.4f);

            //灰烬颜色：深灰到黑
            Color ashColor = Color.Lerp(new Color(60, 50, 45), new Color(30, 20, 15), t) * (alpha * 0.65f * fade);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), ashColor, Rotation, new Vector2(0.5f, 0.5f), Size, SpriteEffects.None, 0f);
        }
    }
}
