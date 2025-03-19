using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityMod.CalamityUtils;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_DWave : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/HollowCircleHardEdge";
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        private float OriginalScale;
        private float FinalScale;
        private Vector2 Squish;
        private Color BaseColor;

        public PRT_DWave(Vector2 position, Vector2 velocity, Color color, Vector2 squish
            , float rotation, float originalScale, float finalScale, int lifeTime) {
            Position = position;
            Velocity = velocity;
            BaseColor = color;
            OriginalScale = originalScale;
            FinalScale = finalScale;
            Scale = originalScale;
            Lifetime = lifeTime;
            Squish = squish;
            Rotation = rotation;
        }

        public override void AI() {
            float pulseProgress = PiecewiseAnimation(LifetimeCompletion, [new CurveSegment(EasingType.PolyOut, 0f, 0f, 1f, 4)]);
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
