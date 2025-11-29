using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 毒雾粒子，用于弥漫的酸性雾气效果
    /// </summary>
    internal class PRT_ToxicMist : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/HeavySmoke";

        [VaultLoaden("@CalamityMod/Particles/BloomCircle")]
        internal static Asset<Texture2D> BloomTex = null;

        private float rotationSpeed;
        private float hueShift;
        private float depthLayer; //0-1, 深度层次
        private Color mistColor;
        private int frameIndex;
        private static int FrameCount = 6;

        public PRT_ToxicMist(Vector2 position, Vector2 velocity, float scale, int lifetime, float depth = 0.5f) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            depthLayer = MathHelper.Clamp(depth, 0f, 1f);
            rotationSpeed = Main.rand.NextFloat(-0.01f, 0.01f);
            hueShift = Main.rand.NextFloat(-0.02f, 0.02f);

            //根据深度选择颜色
            mistColor = depth > 0.6f
                ? new Color(100, 160, 80) //前景 - 亮绿
                : new Color(70, 130, 70); //背景 - 暗绿

            frameIndex = Main.rand.Next(7);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.NonPremultiplied;
            Opacity = 0f;
        }

        public override void AI() {
            //淡入淡出效果
            float fadeIn = Math.Min(Time / 30f, 1f);
            float fadeOut = 1f - (float)Math.Pow(LifetimeCompletion, 2);
            Opacity = fadeIn * fadeOut * (0.3f + depthLayer * 0.4f);

            //缓慢膨胀
            if (LifetimeCompletion < 0.3f) {
                Scale *= 1.008f;
            }
            else {
                Scale *= 0.997f;
            }

            //色相偏移（模拟毒性变化）
            mistColor = Main.hslToRgb(
                (Main.rgbToHsl(mistColor).X + hueShift) % 1,
                Main.rgbToHsl(mistColor).Y,
                Main.rgbToHsl(mistColor).Z
            );

            //旋转
            Rotation += rotationSpeed * (Velocity.X > 0 ? 1f : -1f);

            //速度衰减（雾气漂浮）
            Velocity *= 0.98f;
            
            //轻微上升（毒气特性）
            Velocity.Y -= 0.02f * depthLayer;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (BloomTex == null || BloomTex.IsDisposed) {
                return false;
            }

            Texture2D smokeTexture = PRTLoader.PRT_IDToTexture[ID];
            Texture2D bloomTexture = BloomTex.Value;

            Vector2 drawPos = Position - Main.screenPosition;
            
            //计算当前帧
            int animationFrame = (int)Math.Floor(Time / (float)(Lifetime / (float)FrameCount));
            animationFrame = Math.Clamp(animationFrame, 0, FrameCount - 1);
            Rectangle frame = new Rectangle(80 * frameIndex, 80 * animationFrame, 80, 80);
            Vector2 origin = frame.Size() / 2f;

            Color drawColor = mistColor with { A = 0 } * Opacity;

            //绘制底层扩散光晕
            float bloomScale = Scale * 0.2f * (1f + (1f - depthLayer) * 0.05f);
            spriteBatch.Draw(
                bloomTexture,
                drawPos,
                null,
                drawColor * 0.3f,
                Rotation * 0.5f,
                bloomTexture.Size() / 2f,
                bloomScale,
                SpriteEffects.None,
                0f
            );

            //绘制主烟雾体
            spriteBatch.Draw(
                smokeTexture,
                drawPos,
                frame,
                drawColor,
                Rotation,
                origin,
                Scale,
                SpriteEffects.None,
                0f
            );

            //根据深度绘制额外的毒性发光
            if (depthLayer > 0.5f) {
                float glowIntensity = (depthLayer - 0.5f) * 2f;
                spriteBatch.Draw(
                    smokeTexture,
                    drawPos,
                    frame,
                    new Color(150, 220, 140) * Opacity * glowIntensity * 0.4f,
                    Rotation * 0.8f,
                    origin,
                    Scale * 1.1f,
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }
}
