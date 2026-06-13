using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Others
{
    internal class Bee
    {
        public Projectile OwnerProj;
        public Vector2 Center;
        public Vector2 Velocity;
        public int TimeLife;
        public int FrameIndex;
        public Color Color;
        public float Rotiton;
        public float Scale;
        public float Alpha;
        public bool Active = true;

        public Bee(Projectile proj, Vector2 center, Vector2 velocity, int timelife, Color color, float rotition, float scale, float alpha, int frameIndex) {
            OwnerProj = proj;
            Center = center;
            Velocity = velocity;
            TimeLife = timelife;
            Color = color;
            Rotiton = rotition;
            Scale = scale;
            Alpha = alpha;
            FrameIndex = frameIndex;
        }

        public Bee Clone() {
            return new Bee(OwnerProj, Center, Velocity, TimeLife, Color, Rotiton, Scale, Alpha, FrameIndex);
        }

        public void Update() {
            if (Active) {
                //朝 OwnerProj 转向
                Vector2 directionToOwner = OwnerProj.Center - Center;
                directionToOwner.Normalize();

                //随机扰动速度与朝向
                Velocity = directionToOwner.RotatedBy(MathHelper.ToRadians(Main.rand.Next(-35, 35))) * 3f;
                Rotiton = directionToOwner.ToRotation() + MathHelper.ToRadians(Main.rand.Next(-35, 35));

                //位移
                Center += Velocity;

                TimeLife--;

                Alpha -= 0.01f;

                //寿命耗尽
                if (TimeLife <= 0) {
                    Active = false;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D value) {
            if (Active) {
                Main.EntitySpriteDraw(value, Center - Main.screenPosition, value.GetRectangle(FrameIndex, 4), Color * Alpha, Rotiton, VaultUtils.GetOrig(value, 4), Scale, SpriteEffects.None);
            }
        }
    }
}
