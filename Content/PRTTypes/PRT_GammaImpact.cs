using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 伽马射线冲击粒子
    /// </summary>
    internal class PRT_GammaImpact : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Flashimpact";

        private Color initialColor;
        private float initialScale;
        private float rotationSpeed;
        private bool affectedByGravity;
        public int inOwner = -1;

        //动画参数
        private const int FrameColumns = 4;
        private const int FrameRows = 2;
        private const int TotalFrames = 8;
        private float animationSpeed;

        public PRT_GammaImpact(
            Vector2 position,
            Vector2 velocity,
            Color color,
            float scale,
            int lifetime,
            float rotationSpeed = 0f,
            bool affectedByGravity = false,
            float animSpeed = 0.15f) {
            Position = position;
            Velocity = velocity;
            initialColor = color;
            Color = color;
            initialScale = scale;
            Scale = scale;
            Lifetime = lifetime;
            this.rotationSpeed = rotationSpeed;
            this.affectedByGravity = affectedByGravity;
            animationSpeed = animSpeed;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ai[0] = Main.rand.Next(TotalFrames); // 随机起始帧
        }

        public override void AI() {
            //更新动画帧
            ai[0] += animationSpeed;
            if (ai[0] >= TotalFrames) {
                ai[0] = 0;
            }

            //旋转
            Rotation = Velocity.ToRotation();

            //速度衰减
            Velocity *= 0.95f;

            //重力影响
            if (affectedByGravity && Velocity.Length() < 12f) {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }

            //缩放变化
            float lifeProgress = LifetimeCompletion;
            Scale = initialScale * (float)Math.Sin(lifeProgress * MathHelper.Pi);

            //颜色渐变和淡出
            float fadeProgress = (float)Math.Pow(lifeProgress, 2);
            Color = Color.Lerp(initialColor, Color.Transparent, fadeProgress);

            //添加亮度脉冲
            float pulse = (float)Math.Sin(Time * 0.3f) * 0.3f + 0.7f;
            Opacity = (1f - fadeProgress) * pulse;

            if (inOwner >= 0) {
                Position += Main.player[inOwner].CWR().PlayerPositionChange;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];

            //计算当前帧
            int currentFrame = (int)ai[0];
            int frameX = currentFrame % FrameColumns;
            int frameY = currentFrame / FrameColumns;

            //计算帧的源矩形
            int frameWidth = texture.Width / FrameColumns;
            int frameHeight = texture.Height / FrameRows;
            Rectangle sourceRect = new Rectangle(
                frameX * frameWidth,
                frameY * frameHeight,
                frameWidth,
                frameHeight
            );

            Vector2 origin = new Vector2(frameWidth, frameHeight) * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;

            //绘制发光层
            spriteBatch.Draw(
                texture,
                drawPosition,
                sourceRect,
                Color * Opacity * 0.5f,
                Rotation,
                origin,
                Scale * 1.2f,
                SpriteEffects.None,
                0f
            );

            //绘制主体
            spriteBatch.Draw(
                texture,
                drawPosition,
                sourceRect,
                Color * Opacity,
                Rotation,
                origin,
                Scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
