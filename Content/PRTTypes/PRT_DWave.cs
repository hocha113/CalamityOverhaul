using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_DWave : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        private float OriginalScale;
        private float FinalScale;
        private Vector2 Squish;
        private Color BaseColor;

        public void Configure(Vector2 squish, float rotation, float finalScale, int lifeTime) {
            BaseColor = Color;
            OriginalScale = Scale;
            Squish = squish;
            Rotation = rotation;
            FinalScale = finalScale;
            Lifetime = lifeTime;
        }

        public override void Reset() {
            base.Reset();
            OriginalScale = 0f;
            FinalScale = 0f;
            Squish = default;
            BaseColor = default;
        }

        public override void AI() {
            float pulseProgress = LifetimeCompletion;
            Scale = MathHelper.Lerp(OriginalScale, FinalScale, pulseProgress);

            Opacity = (float)Math.Sin(MathHelper.PiOver2 + LifetimeCompletion * MathHelper.PiOver2);

            Color = BaseColor * Opacity;
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity, Rotation
                , tex.Size() / 2f, Scale * Squish, SpriteEffects.None, 0);
            return false;
        }
    }
}
