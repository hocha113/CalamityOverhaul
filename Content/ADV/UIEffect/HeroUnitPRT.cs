using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>英雄面板椭圆轨道星尘粒子</summary>
    public class HeroUnitPRT(Vector2 center, float orbitRadius)
    {
        public Vector2 Center = center;
        public float OrbitRadius = orbitRadius;
        public float Angle = Main.rand.NextFloat(MathHelper.TwoPi);
        public float AngularSpeed = Main.rand.NextFloat(0.008f, 0.018f) * (Main.rand.NextBool() ? 1f : -1f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(140f, 260f);
        public float Size = Main.rand.NextFloat(1.5f, 3.5f);
        public float Seed = Main.rand.NextFloat(10f);
        public float EccentricityY = Main.rand.NextFloat(0.35f, 0.7f);

        public bool Update() {
            Life++;
            Angle += AngularSpeed;
            return Life >= MaxLife;
        }

        public Vector2 GetPosition() {
            float x = Center.X + MathF.Cos(Angle) * OrbitRadius;
            float y = Center.Y + MathF.Sin(Angle) * OrbitRadius * EccentricityY;
            float drift = MathF.Sin(Life * 0.025f + Seed * 4f) * 3f;
            return new Vector2(x + drift, y);
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = MathF.Sin(t * MathHelper.Pi) * alpha;
            float pulse = MathF.Sin((Life + Seed * 18f) * 0.055f) * 0.5f + 0.5f;
            float scale = Size * (0.65f + pulse * 0.45f);
            Vector2 pos = GetPosition();

            //蓝金渐变核心
            Color blueGold = Color.Lerp(
                new Color(100, 160, 255),
                new Color(255, 210, 100),
                pulse) * (0.7f * fade);
            sb.Draw(px, pos, null, blueGold, Angle, new Vector2(0.5f),
                new Vector2(scale * 1.8f, scale * 0.3f), SpriteEffects.None, 0f);

            //中心亮点
            Color bright = new Color(255, 245, 220) * (0.5f * fade * pulse);
            sb.Draw(px, pos, null, bright, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.4f), SpriteEffects.None, 0f);
        }
    }
}
