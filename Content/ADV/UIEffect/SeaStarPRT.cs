using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// 海洋星光粒子
    /// </summary>
    public class SeaStarPRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float BaseRadius = Main.rand.NextFloat(2f, 4f);
        public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(60f, 140f);
        public float Seed = Main.rand.NextFloat(10f);

        public bool Update() {
            Life++;
            Rot += 0.02f;
            float t = Life / MaxLife;
            float drift = (float)Math.Sin((Life + Seed * 20f) * 0.03f) * 6f;
            Pos.X += drift * 0.02f;
            return Life >= MaxLife;
        }

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Rot += 0.02f;
            float drift = (float)Math.Sin((Life + Seed * 20f) * 0.03f) * 6f;
            Pos.X += drift * 0.02f;
            if (Life >= MaxLife) return true;
            if (Pos.X < panelPos.X - 40 || Pos.X > panelPos.X + panelSize.X + 40 || Pos.Y < panelPos.Y - 40 || Pos.Y > panelPos.Y + panelSize.Y + 40) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float a) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi) * a;
            float scale = 2.5f + (float)Math.Sin((Life + Seed * 30f) * 0.1f) * 1.2f;
            Color c = Color.Gold * (0.6f * fade);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
        }

        public void DrawEnhanced(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
            float scale = BaseRadius * (0.6f + (float)Math.Sin((Life + Seed * 33f) * 0.08f) * 0.4f);
            Color c = Color.Gold * (0.7f * fade);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
        }
    }
}
