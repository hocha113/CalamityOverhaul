using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;
using static CalamityMod.CalamityUtils;

namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_StarPulseRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        private float OriginalScale;
        private float FinalScale;
        private float opacity;
        private Color BaseColor;
        public PRT_StarPulseRing(Vector2 position, Vector2 velocity, Color color
            , float originalScale, float finalScale, int lifeTime) {
            Position = position;
            Velocity = velocity;
            BaseColor = color;
            OriginalScale = originalScale;
            FinalScale = finalScale;
            Scale = originalScale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            SetLifetime = true;
        }
        public override void AI() {
            float pulseProgress = PiecewiseAnimation(LifetimeCompletion, [new CurveSegment(EasingType.PolyOut, 0f, 0f, 1f, 4)]);
            Scale = MathHelper.Lerp(OriginalScale, FinalScale, pulseProgress);

            opacity = (float)Math.Sin(MathHelper.PiOver2 + LifetimeCompletion * MathHelper.PiOver2);
            Color = BaseColor * opacity;
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * opacity, Rotation, tex.Size() / 2f, Scale, SpriteEffects.None, 0);

            Texture2D star = ModContent.Request<Texture2D>("CalamityMod/Particles/ThinSparkle").Value;
            Texture2D bloom = ModContent.Request<Texture2D>("CalamityMod/Particles/BloomCircle").Value;
            float properBloomSize = star.Height / (float)bloom.Height;
            spriteBatch.Draw(bloom, pos, null, Color * 0.5f, 0, bloom.Size() / 2f, Scale * properBloomSize * 3, SpriteEffects.None, 0);
            spriteBatch.Draw(star, pos, null, Color, 0, star.Size() / 2f, Scale * 3, SpriteEffects.None, 0);

            return false;
        }
    }
}
