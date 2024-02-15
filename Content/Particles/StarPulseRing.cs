using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using static CalamityMod.CalamityUtils;
using System;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Particles
{
    internal class StarPulseRing : CWRParticle
    {
        public override string Texture => "CalamityMod/Particles/HollowCircleHardEdge";
        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;
        public override bool SetLifetime => true;

        private float OriginalScale;
        private float FinalScale;
        private float opacity;
        private Color BaseColor;

        public StarPulseRing(Vector2 position, Vector2 velocity, Color color, float originalScale, float finalScale, int lifeTime) {
            Position = position;
            Velocity = velocity;
            BaseColor = color;
            OriginalScale = originalScale;
            FinalScale = finalScale;
            Scale = originalScale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update() {
            float pulseProgress = PiecewiseAnimation(LifetimeCompletion, new CurveSegment[] { new CurveSegment(EasingType.PolyOut, 0f, 0f, 1f, 4) });
            Scale = MathHelper.Lerp(OriginalScale, FinalScale, pulseProgress);

            opacity = (float)Math.Sin(MathHelper.PiOver2 + LifetimeCompletion * MathHelper.PiOver2);
            Color = BaseColor * opacity;
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;
        }

        public override void CustomDraw(SpriteBatch spriteBatch) {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * opacity, Rotation, tex.Size() / 2f, Scale, SpriteEffects.None, 0);

            Texture2D star = ModContent.Request<Texture2D>("CalamityMod/Particles/ThinSparkle").Value;
            Texture2D bloom = ModContent.Request<Texture2D>("CalamityMod/Particles/BloomCircle").Value;
            float properBloomSize = (float)star.Height / (float)bloom.Height;
            spriteBatch.Draw(bloom, pos, null, Color * 0.5f, 0, bloom.Size() / 2f, Scale * properBloomSize * 3, SpriteEffects.None, 0);
            spriteBatch.Draw(star, pos, null, Color, 0, star.Size() / 2f, Scale * 3, SpriteEffects.None, 0);
        }

        public override void CustomDraw(SpriteBatch spriteBatch, Vector2 basePosition) {
            Texture2D tex = ModContent.Request<Texture2D>("CalamityMod/Particles/HollowCircleHardEdge").Value;

        }
    }
}
