using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>伽马冲击</summary>
    internal class PRT_GammaImpact : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Flashimpact";

        private Color initialColor;
        private float initialScale;
        private float rotationSpeed;
        private bool affectedByGravity;
        public int inOwner = -1;

        private const int FrameColumns = 4;
        private const int FrameRows = 2;
        private const int TotalFrames = 8;
        private float animationSpeed;

        public PRT_GammaImpact() {
            animationSpeed = 0.15f;
        }
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

        public override bool CanPool => true;
        public void Configure(int lt, float rotationSpeed = 0f, bool affectedByGravity = false, float animSpeed = 0.15f) {
            Lifetime = lt;
            initialColor = Color;
            initialScale = Scale;
            this.rotationSpeed = rotationSpeed;
            this.affectedByGravity = affectedByGravity;
            animationSpeed = animSpeed;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            initialScale = 0f;
            rotationSpeed = 0f;
            affectedByGravity = false;
            animationSpeed = 0.15f;
            inOwner = -1;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            ai[0] = Main.rand.Next(TotalFrames); //随机起始帧
        }

        public override void AI() {
            ai[0] += animationSpeed;
            if (ai[0] >= TotalFrames) {
                ai[0] = 0;
            }

            Rotation = Velocity.ToRotation();

            Velocity *= 0.95f;

            if (affectedByGravity && Velocity.Length() < 12f) {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }

            float lifeProgress = LifetimeCompletion;
            Scale = initialScale * (float)Math.Sin(lifeProgress * MathHelper.Pi);

            float fadeProgress = (float)Math.Pow(lifeProgress, 2);
            Color = Color.Lerp(initialColor, new Color(50, 30, 120, 0), fadeProgress);

            float pulse = (float)Math.Sin(Time * 0.3f) * 0.3f + 0.7f;
            Opacity = (1f - fadeProgress) * pulse;

            if (inOwner >= 0) {
                Position += Main.player[inOwner].CWR().PlayerPositionChange;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];

            int currentFrame = (int)ai[0];
            int frameX = currentFrame % FrameColumns;
            int frameY = currentFrame / FrameColumns;

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
