using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    /// <summary>
    /// 霓虹女仆粒子：微小的发光菱形碎片，缓慢上升并旋转，带有霓虹蓝紫色辉光<br/>
    /// 用于SHPC赛博女仆风格对话框的氛围装饰
    /// </summary>
    public class NeonMaidPRT(Vector2 p)
    {
        public Vector2 Pos = p;
        public float Size = Main.rand.NextFloat(1.2f, 3f);
        public float Rot = Main.rand.NextFloat(MathHelper.TwoPi);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(90f, 160f);
        public float Seed = Main.rand.NextFloat(10f);
        public Vector2 Velocity = new(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.45f, -0.12f));
        // 在霓虹蓝和浅紫之间随机偏移
        public float ColorLerp = Main.rand.NextFloat(1f);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            Rot += 0.018f;
            Pos += Velocity;
            // 轻微漂移感
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

            // 霓虹蓝到浅紫渐变
            Color neonBlue = new Color(60, 140, 255);
            Color neonViolet = new Color(150, 100, 240);
            Color c = Color.Lerp(neonBlue, neonViolet, ColorLerp) * (0.7f * fade);

            // 菱形由两个十字线叠加表现
            sb.Draw(px, Pos, null, c, Rot, new Vector2(0.5f),
                new Vector2(scale * 1.8f, scale * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(px, Pos, null, c * 0.85f, Rot + MathHelper.PiOver2, new Vector2(0.5f),
                new Vector2(scale * 1.8f, scale * 0.25f), SpriteEffects.None, 0f);
            // 中心亮点
            sb.Draw(px, Pos, null, c * 0.5f, 0f, new Vector2(0.5f),
                new Vector2(scale * 0.35f), SpriteEffects.None, 0f);
        }
    }
}
