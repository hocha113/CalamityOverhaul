using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 酸液飞溅粒子，用于气泡破裂和硫酸爆发效果
    /// </summary>
    internal class PRT_AcidSplash : BasePRT
    {
        public override string Texture => "CalamityMod/Projectiles/StarProj";

        private Color acidColor;
        private bool affectedByGravity;
        private float stretchFactor; //拉伸因子

        public PRT_AcidSplash(Vector2 position, Vector2 velocity, float scale, int lifetime, bool gravity = true) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            affectedByGravity = gravity;

            //随机酸液颜色
            acidColor = Main.rand.Next(4) switch {
                0 => new Color(110, 200, 120),
                1 => new Color(90, 180, 100),
                2 => new Color(130, 220, 140),
                _ => new Color(100, 190, 110)
            };
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 1f;
        }

        public override void AI() {
            //颜色渐变到更深的绿色
            Color = Color.Lerp(acidColor, new Color(60, 120, 70), LifetimeCompletion);

            //透明度变化
            Opacity = 1f - (float)Math.Pow(LifetimeCompletion, 2);

            //重力影响
            if (affectedByGravity) {
                Velocity.Y += 0.15f;
                Velocity.X *= 0.98f;
            }
            else {
                Velocity *= 0.96f;
            }

            //根据速度计算拉伸
            stretchFactor = MathHelper.Clamp(Velocity.Length() / 5f, 0.5f, 3f);

            //旋转朝向速度方向
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //尺寸衰减
            Scale *= 0.97f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            //拉伸向量
            Vector2 scaleVec = new Vector2(Scale * 0.6f, Scale * stretchFactor * 1.8f);

            //主飞溅体
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color * Opacity,
                Rotation,
                origin,
                scaleVec,
                SpriteEffects.None,
                0f
            );

            //发光层
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color * Opacity * 0.5f,
                Rotation,
                origin,
                scaleVec * new Vector2(0.7f, 1.1f),
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
