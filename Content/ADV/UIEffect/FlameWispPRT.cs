using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class FlameWispPRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public Vector2 Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.3f, 0.8f);
        public float Size = Main.rand.NextFloat(8f, 16f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(120f, 200f);
        public float Seed = Main.rand.NextFloat(10f);
        public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            float t = Life / MaxLife;

            //漂浮运动
            Phase += 0.08f;
            Vector2 drift = new Vector2(
                (float)Math.Sin(Phase + Seed) * 0.5f,
                (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
            );
            Pos += Velocity + drift;

            //边界检查
            if (Pos.X < panelPos.X + 20f || Pos.X > panelPos.X + panelSize.X - 20f) {
                Velocity.X *= -0.8f;
            }
            if (Pos.Y < panelPos.Y + 40f || Pos.Y > panelPos.Y + panelSize.Y - 40f) {
                Velocity.Y *= -0.8f;
            }

            if (Life >= MaxLife) {
                return true;
            }
            return false;
        }

        public bool Update(Vector2 center, float radius) {
            Life++;
            Phase += 0.08f;
            Vector2 drift = new Vector2(
                (float)Math.Sin(Phase + Seed) * 0.5f,
                (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
            );
            Pos += Velocity + drift;

            Vector2 toCenter = center - Pos;
            if (toCenter.Length() > radius) {
                Velocity = toCenter * 0.01f;
            }

            return Life >= MaxLife;
        }

        public bool Update(Vector2 basePos) {
            Life++;
            float t = Life / MaxLife;

            //漂浮运动
            Phase += 0.08f;
            Vector2 drift = new Vector2(
                (float)Math.Sin(Phase + Seed) * 0.5f,
                (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
            );
            Pos += Velocity + drift;

            //边界检查（相对于面板）
            if (Pos.X < basePos.X - 100f || Pos.X > basePos.X + 100f) {
                Velocity.X *= -0.8f;
            }
            if (Pos.Y < basePos.Y - 60f || Pos.Y > basePos.Y + 60f) {
                Velocity.Y *= -0.8f;
            }

            if (Life >= MaxLife) {
                return true;
            }
            return false;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * Math.PI);
            float pulse = (float)Math.Sin(Life * 0.15f + Seed) * 0.5f + 0.5f;

            float scale = Size * (0.8f + pulse * 0.4f);

            //火焰精灵颜色
            Color wispCore = new Color(255, 200, 120) * (alpha * 0.6f * fade);
            Color wispGlow = new Color(255, 120, 60) * (alpha * 0.3f * fade);

            //外层光晕
            sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), wispGlow, 0f, new Vector2(0.5f, 0.5f), scale * 3f, SpriteEffects.None, 0f);
            //内层核心
            sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), wispCore, 0f, new Vector2(0.5f, 0.5f), scale * 1.2f, SpriteEffects.None, 0f);
        }
    }
}
