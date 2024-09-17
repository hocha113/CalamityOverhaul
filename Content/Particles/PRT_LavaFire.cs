using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_LavaFire : BasePRT, ICWRLoader
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle3";
        //public override bool SetLifetime => true;
        //public override bool UseCustomDraw => true;
        //public override bool UseAdditiveBlend => true;
        public Color[] colors;
        public int timer;
        public float speedX;
        public float mult;
        public int timeLeftMax;
        public float size;
        public int minLifeTime;
        public int maxLifeTime;
        private float opacity;
        private float timeLife;
        private static Asset<Texture2D> StarTexture;
        private static Asset<Texture2D> SoftGlow;
        void ICWRLoader.LoadAsset() {
            StarTexture = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarTexture");
            SoftGlow = CWRUtils.GetT2DAsset(CWRConstant.Masking + "SoftGlow");
        }
        void ICWRLoader.UnLoadData() {
            StarTexture = null;
            SoftGlow = null;
        }
        public override void SetPRT() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            SetLifetime = true;

            if (colors == null) {
                colors = new Color[3];
                colors[0] = new Color(262, 150, 45, 255);//明
                colors[1] = new Color(186, 35, 24, 255);//过渡
                colors[2] = new Color(122, 24, 36, 255);//暗，渐变目标
            }
            
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
            if (ai[0] > 0) {
                ai[0]--;
                return;
            }

            opacity = MathHelper.Lerp(1f, 0f, (timeLeftMax / 2f - timeLife) / (timeLeftMax / 2f));

            if (ai[1] == 1) {
                return;
            }
            else if (ai[1] == 2) {
                Velocity *= 0.9f;
            }
            else {
                if (timer == 0) {
                    timer = Main.rand.Next(50, 100);
                    speedX = Main.rand.NextFloat(4f, 9f);
                    mult = Main.rand.NextFloat(10f, 31f) / 200f;
                }

                float sineX = (float)Math.Sin(Main.GlobalTimeWrappedHourly * speedX);
                Velocity += new Vector2(Main.windSpeedCurrent * (Main.windPhysicsStrength * 3f) * MathHelper.Lerp(1f, 0.1f, Math.Abs(Velocity.X) / 6f), 0f);
                Velocity += new Vector2(sineX * mult, -Main.rand.NextFloat(1f, 2f) / 100f);
                Velocity = new Vector2(MathHelper.Clamp(Velocity.X, -6f, 6f), MathHelper.Clamp(Velocity.Y, -6f, 6f));
            }

            timer--;
            timeLife--;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex1 = PRTLoader.PRT_IDToTexture[ID];
            Texture2D tex2 = StarTexture.Value;
            Texture2D tex3 = SoftGlow.Value;

            Color emberColor = Color.Lerp(colors[0], colors[2], (float)(timeLeftMax - timeLife) / timeLeftMax) * opacity;
            Color glowColor = Color.Lerp(colors[1], colors[2], (float)(timeLeftMax - timeLife) / timeLeftMax);
            float pixelRatio = 1f / 64f;

            Vector2 drawPos = Position - Main.screenPosition;
            spriteBatch.Draw(tex3, drawPos, new Rectangle(0, 0, 64, 64), glowColor, Rotation,
                             new Vector2(32f, 32f), size * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex1, drawPos - new Vector2(1.5f, 1.5f), new Rectangle(0, 0, 64, 64), emberColor,
                             Rotation, Vector2.Zero, pixelRatio * 3f * size * Scale, SpriteEffects.None, 0f);

            if (ai[1] < 1) {
                spriteBatch.Draw(tex2, drawPos, null, Color, Rotation, tex2.Size() / 2, Scale * 0.04f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
