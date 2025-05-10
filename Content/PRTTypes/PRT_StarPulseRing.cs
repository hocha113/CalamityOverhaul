using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_StarPulseRing : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        private float OriginalScale;
        private float FinalScale;
        private Color BaseColor;
        [VaultLoaden("@CalamityMod/Particles/ThinSparkle")]
        internal static Asset<Texture2D> ThinSparkle = null;
        [VaultLoaden("@CalamityMod/Particles/BloomCircle")]
        internal static Asset<Texture2D> BloomCircle = null;
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
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
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
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, tex.Size() / 2f, Scale, SpriteEffects.None, 0);
            Texture2D star = ThinSparkle.Value;
            Texture2D bloom = BloomCircle.Value;
            float properBloomSize = star.Height / (float)bloom.Height;
            spriteBatch.Draw(bloom, pos, null, Color * 0.5f, 0, bloom.Size() / 2f, Scale * properBloomSize * 3, SpriteEffects.None, 0);
            spriteBatch.Draw(star, pos, null, Color, 0, star.Size() / 2f, Scale * 3, SpriteEffects.None, 0);
            return false;
        }
    }
}
