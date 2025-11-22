using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// 科技粒子类
    /// </summary>
    public class TechPRT
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Rotation;

        public TechPRT(Vector2 pos) {
            Position = pos;
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float speed = Main.rand.NextFloat(0.3f, 1f);
            Velocity = angle.ToRotationVector2() * speed;
            Life = 0f;
            MaxLife = Main.rand.NextFloat(60f, 120f);
            Size = Main.rand.NextFloat(1.5f, 3f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public bool Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.98f;
            Rotation += 0.03f;

            return Life >= MaxLife;
        }

        public void Draw(SpriteBatch sb, float baseAlpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * MathHelper.Pi);
            float alpha = fade * baseAlpha * 0.6f;

            Color color = new Color(80, 200, 255) * alpha;

            sb.Draw(pixel, Position, new Rectangle(0, 0, 1, 1), color, Rotation,
                new Vector2(0.5f), new Vector2(Size * 2f, Size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, Position, new Rectangle(0, 0, 1, 1), color * 0.8f, Rotation + MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(Size * 2f, Size * 0.3f), SpriteEffects.None, 0f);
        }
    }
}
