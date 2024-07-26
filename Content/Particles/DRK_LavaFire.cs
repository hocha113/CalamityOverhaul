using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul.Content.Particles
{
    internal class DRK_LavaFire : BaseParticle
    {
        public override string Texture => CWRConstant.Masking + "Circle";
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;
        public int timer;
        public float speedX;
        public float mult;
        public int timeLeftMax;
        public float size;
        public int minLifeTime;
        public int maxLifeTime;
        float opacity;
        float timeLife;
        public override void SetDRK() {
            if (minLifeTime == 0) {
                minLifeTime = 90;
            }
            if (maxLifeTime == 0) {
                maxLifeTime = 121;
            }
            timeLife = timer = Lifetime = Main.rand.Next(minLifeTime, maxLifeTime);
            timer = (int)(timer * Main.rand.NextFloat(0.6f, 1.1f));
            speedX = Main.rand.NextFloat(4f, 9f);
            mult = Main.rand.NextFloat(10f, 31f) / 200f;
            size = Main.rand.NextFloat(5f, 11f) / 10f;
            if (ai[1] == 1) {
                Lifetime /= 7;
            }
            timeLeftMax = Lifetime;
        }

        public override void AI() {
            if (ai[0] <= 0) {
                opacity = MathHelper.Lerp(1f, 0f, (float)(timeLeftMax / 2f - timeLife) / (timeLeftMax / 2f));
                if (ai[1] is 1) {
                    return;
                }
                if (ai[1] is 2) {
                    Velocity *= 0.9f;
                }
                else {
                    float sineX = (float)Math.Sin(Main.GlobalTimeWrappedHourly * speedX);
                    if (timer == 0) {
                        timer = Main.rand.Next(50, 100);
                        speedX = Main.rand.NextFloat(4f, 9f);
                        mult = Main.rand.NextFloat(10f, 31f) / 200f;
                    }
                    Velocity += new Vector2(Main.windSpeedCurrent * (Main.windPhysicsStrength * 3f) * MathHelper.Lerp(1f, 0.1f, Math.Abs(Velocity.X) / 6f), 0f);
                    Velocity += new Vector2(sineX * mult, -Main.rand.NextFloat(1f, 2f) / 100f);
                    Utils.Clamp(Velocity.X, -6f, 6f);
                    Utils.Clamp(Velocity.Y, -6f, 6f);
                }
                timer--;
                timeLife--;
            }
            ai[0]--;
        }

        public override void CustomDraw(SpriteBatch spriteBatch) {
            Texture2D circle = ModContent.Request<Texture2D>(Texture).Value;
            Texture2D ember = ModContent.Request<Texture2D>(CWRConstant.Masking + "StarTexture").Value;
            Texture2D glow = ModContent.Request<Texture2D>(CWRConstant.Masking + "SoftGlow").Value;

            Color bright = Color.Multiply(new(260, 149, 46, 255), opacity);
            Color mid = Color.Multiply(new(187, 33, 25, 255), opacity);
            Color dark = Color.Multiply(new(121, 23, 37, 255), opacity);
            Color emberColor = Color.Multiply(Color.Lerp(bright, dark, (float)(timeLeftMax - timeLife) / timeLeftMax), opacity);
            Color glowColor = Color.Multiply(Color.Lerp(mid, dark, (float)(timeLeftMax - timeLife) / timeLeftMax), 1f);
            float pixelRatio = 1f / 64f;

            Vector2 drawPos = Position - Main.screenPosition;
            //spriteBatch.Draw(glow, drawPos, null, Color.White, 0, Vector2.Zero, 6, SpriteEffects.None, 0);

            spriteBatch.Draw(glow, drawPos, new Rectangle(0, 0, 64, 64), glowColor, Rotation
                , new Vector2(32f, 32f), 1f * size * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(circle, drawPos - new Vector2(1.5f, 1.5f), new Rectangle(0, 0, 64, 64), emberColor
                , Rotation, Vector2.Zero, 1f * pixelRatio * 3f * size * Scale, SpriteEffects.None, 0f);

            if (ai[1] < 1) {
                spriteBatch.Draw(ember, drawPos, null, Color, Rotation
                    , ember.Size() / 2, 1f * Scale * 0.04f, SpriteEffects.None, 0f);
            }
        }
    }
}
