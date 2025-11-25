using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Note : BasePRT
    {
        public override string Texture => CWRConstant.Placeholder;

        private int noteType;
        private float floatOffset;
        private float pulseTimer;

        public PRT_Note(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale, int noteType = -1) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = scale;
            Lifetime = lifetime;
            this.noteType = noteType >= 0 ? noteType : Main.rand.Next(3);
            floatOffset = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            //淡入效果
            if (Time < 10) {
                Opacity = Time / 10f;
            }
            //淡出效果
            else if (LifetimeCompletion > 0.7f) {
                Opacity = 1f - (LifetimeCompletion - 0.7f) / 0.3f;
            }
            else {
                Opacity = 1f;
            }

            //旋转动画
            Rotation = Velocity.ToRotation();

            //轻微的上下浮动
            pulseTimer += 0.1f;
            float verticalWave = MathF.Sin(pulseTimer + floatOffset) * 0.5f;
            Velocity.Y += verticalWave * 0.01f;

            //速度衰减
            Velocity *= 0.97f;

            //缩放脉动
            float pulseFactor = 1f + MathF.Sin(pulseTimer * 2f) * 0.1f;
            Scale *= 0.995f; //逐渐缩小
            Scale *= pulseFactor; //脉动效果
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            //加载对应的音符弹幕纹理
            int projType = noteType switch {
                0 => ProjectileID.TiedEighthNote,
                1 => ProjectileID.EighthNote,
                2 => ProjectileID.QuarterNote,
                _ => ProjectileID.EighthNote
            };

            Main.instance.LoadProjectile(projType);
            Texture2D texture = TextureAssets.Projectile[projType].Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Color drawColor = Color * Opacity;

            //绘制主体
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                drawColor,
                Rotation,
                texture.Size() / 2f,
                Scale,
                SpriteEffects.None,
                0f
            );

            //添加一个微弱的外发光效果
            float glowScale = Scale * 1.2f;
            Color glowColor = Color * Opacity * 0.3f;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                glowColor,
                Rotation,
                texture.Size() / 2f,
                glowScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
