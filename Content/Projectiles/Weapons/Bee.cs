using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons
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
                // 根据OwnerProj的位置调整速度
                Vector2 directionToOwner = OwnerProj.Center - Center;
                directionToOwner.Normalize();

                // 随机改变速度和旋转角度
                Velocity = directionToOwner.RotatedBy(MathHelper.ToRadians(Main.rand.Next(-35, 35))) * 3f;
                Rotiton = directionToOwner.ToRotation() + MathHelper.ToRadians(Main.rand.Next(-35, 35));

                // 更新位置
                Center += Velocity;

                // 减少生命周期
                TimeLife--;

                Alpha -= 0.01f;

                // 如果生命周期小于等于0，标记为不活跃
                if (TimeLife <= 0) {
                    Active = false;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D value) {
            if (Active) {
                Main.EntitySpriteDraw(value, Center - Main.screenPosition, CWRUtils.GetRec(value, FrameIndex, 4), Color * Alpha, Rotiton, CWRUtils.GetOrig(value, 4), Scale, SpriteEffects.None);
            }
        }
    }
}
