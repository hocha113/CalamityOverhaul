using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 硫磺海毒液气泡粒子，专门用于从海底上升的气泡效果
    /// </summary>
    internal class PRT_ToxicBubble : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/BloomCircle";

        private float popProgress; //气泡破裂进度
        private float shimmerTimer; //表面波光粼粼计时器
        private float floatWobble; //上升时的横向摆动
        private Color coreColor; //气泡核心颜色
        private Color rimColor; //气泡边缘颜色
        private bool isPopping; //是否正在破裂

        public PRT_ToxicBubble(Vector2 position, Vector2 velocity, float scale, int lifetime) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            
            //随机毒绿色调
            coreColor = Main.rand.NextBool() 
                ? new Color(120, 220, 140, 120)
                : new Color(150, 200, 100, 140);
            rimColor = new Color(180, 240, 160, 200);
            
            floatWobble = Main.rand.NextFloat(MathHelper.TwoPi);
            shimmerTimer = Main.rand.NextFloat(MathHelper.TwoPi);
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
            //接近水面时开始破裂
            else if (LifetimeCompletion > 0.85f) {
                if (!isPopping) {
                    isPopping = true;
                    popProgress = 0f;
                }
                popProgress = (LifetimeCompletion - 0.85f) / 0.15f;
                Opacity = 1f - popProgress;
            }
            else {
                Opacity = 1f;
            }

            //横向摆动（模拟水流影响）
            shimmerTimer += 0.15f;
            floatWobble += 0.08f;
            float wobbleX = MathF.Sin(floatWobble) * 0.12f;
            Velocity.X += wobbleX;

            //气泡上升时的速度衰减和浮力
            Velocity.Y *= 0.985f;
            Velocity.X *= 0.95f;

            //破裂时的效果
            if (isPopping) {
                //气泡扁平化
                Scale *= 1.05f;
                
                //生成破裂飞溅粒子
                if (Main.rand.NextBool(3)) {
                    Vector2 splashVel = Main.rand.NextVector2Circular(2f, 2f);
                    splashVel.Y -= 1f;
                    
                    PRT_AcidSplash splash = new PRT_AcidSplash(
                        Position,
                        splashVel,
                        Main.rand.NextFloat(0.3f, 0.6f),
                        Main.rand.Next(20, 40)
                    );
                    PRTLoader.AddParticle(splash);
                }
            }
            else {
                //正常气泡的轻微膨胀收缩
                Scale *= 1f + MathF.Sin(shimmerTimer) * 0.002f;
            }

            Rotation += 0.01f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            //破裂时的扁平化效果
            Vector2 scaleVector = isPopping 
                ? new Vector2(Scale * (1f + popProgress * 0.5f), Scale * (1f - popProgress * 0.3f))
                : new Vector2(Scale);

            //绘制气泡外圈（边缘高光）
            float rimPulse = MathF.Sin(shimmerTimer * 1.5f) * 0.3f + 0.7f;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                rimColor * Opacity * rimPulse,
                Rotation,
                origin,
                scaleVector * 1.2f,
                SpriteEffects.None,
                0f
            );

            //绘制气泡核心
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                coreColor * Opacity,
                Rotation,
                origin,
                scaleVector,
                SpriteEffects.None,
                0f
            );

            //绘制表面波光效果
            float shimmer = MathF.Sin(shimmerTimer * 2f) * 0.5f + 0.5f;
            Vector2 shimmerOffset = new Vector2(
                MathF.Cos(shimmerTimer) * Scale * 0.2f,
                MathF.Sin(shimmerTimer * 0.7f) * Scale * 0.15f
            );

            spriteBatch.Draw(
                texture,
                drawPos + shimmerOffset,
                null,
                Color.White * Opacity * shimmer * 0.4f,
                Rotation * 0.5f,
                origin,
                scaleVector * 0.4f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
