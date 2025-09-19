using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Nutritional : BasePRT
    {
        //定义粒子使用的纹理路径
        public override string Texture => CWRConstant.Masking + "Extra_98";

        //设置粒子的额外属性
        public override void SetProperty() {
            Color = new Color(113, 224, 88);

            if (Lifetime == 0f) {
                Lifetime = Main.rand.Next(60, 90);
            }
            
            if (Scale == 0f) {
                Scale = 1f;
            }

            Scale *= Main.rand.NextFloat(1.2f, 2.2f);
            Velocity.Y += Main.rand.NextFloat(-6, 2);
            Velocity.X += Main.rand.NextFloat(-2, 2);
        }

        //定义粒子每一帧的行为
        public override void AI() {
            //给粒子一个轻微的向下的加速度，模拟重力效果
            Velocity.Y += 0.02f;
            //让粒子的速度每一帧都衰减，模拟空气阻力
            Velocity *= 0.96f;

            //随着生命周期的推移，粒子会逐渐变小
            Scale *= 0.98f;
        }

        //在默认绘制之前执行的自定义绘制方法
        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPosition = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            spriteBatch.Draw(tex, drawPosition, null, Color, Rotation, origin, Scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}