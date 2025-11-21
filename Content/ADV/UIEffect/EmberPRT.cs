using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.UIEffect
{
    public class EmberPRT(Vector2 start)
    {
        public Vector2 Pos = start;
        public float Size = Main.rand.NextFloat(2.5f, 5.5f);
        public float RiseSpeed = Main.rand.NextFloat(0.4f, 1.1f);
        public float Drift = Main.rand.NextFloat(-0.25f, 0.25f);
        public float Life = 0f;
        public float MaxLife = Main.rand.NextFloat(70f, 130f);
        public float Seed = Main.rand.NextFloat(10f);
        public float RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f);
        public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

        public EmberPRT(Vector2 start, float size, float riseSpeed, float drift, float life, float maxLife) : this(start) {
            Pos = start;
            Size = size;
            RiseSpeed = riseSpeed;
            Drift = drift;
            Life = life;
            MaxLife = maxLife;
        }

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (1f - t * 0.3f);
            Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
            Rotation += RotationSpeed;

            if (Life >= MaxLife || Pos.Y < panelPos.Y + 15f) {
                return true;
            }
            return false;
        }

        public bool Update(Vector2 basePos) {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (1f - t * 0.3f);
            Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
            Rotation += RotationSpeed;

            if (Life >= MaxLife || Pos.Y < basePos.Y - 80f) {
                return true;
            }
            return false;
        }

        public bool Update() {
            Life++;
            float t = Life / MaxLife;
            Pos.Y -= RiseSpeed * (1f - t * 0.3f);
            Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
            Rotation += RotationSpeed;
            return Life >= MaxLife;
        }

        public void Draw(SpriteBatch sb, float alpha) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float t = Life / MaxLife;
            float fade = (float)Math.Sin(t * Math.PI);
            float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);

            //火焰余烬颜色：橙红到深红
            Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
            Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);

            //光晕
            sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
            //核心
            sb.Draw(vaule, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
        }
    }
}
