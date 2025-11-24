using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Sparkle : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Sparkle";
        public bool UseAltVisual = true;

        public bool imporant;
        private float Spin;
        private float opacity;
        private Color Bloom;
        private Color LightColor => Bloom * opacity;
        private float BloomScale;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public PRT_Sparkle(Vector2 position, Vector2 velocity, Color color, Color bloom, float scale, int lifeTime
            , float rotationSpeed = 0f, float bloomScale = 1f, bool AddativeBlend = true, bool needed = false) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Bloom = bloom;
            Scale = scale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Spin = rotationSpeed;
            BloomScale = bloomScale;
            UseAltVisual = AddativeBlend;
            imporant = needed;
        }

        public override void AI() {
            opacity = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            Lighting.AddLight(Position, LightColor.R / 255f, LightColor.G / 255f, LightColor.B / 255f);
            Velocity *= 0.95f;
            Rotation += Spin * ((Velocity.X > 0) ? 1f : -1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D starTexture = ModContent.Request<Texture2D>(Texture).Value;
            Texture2D bloomTexture = PRT_Light.BloomTex.Value;
            float properBloomSize = (float)starTexture.Height / bloomTexture.Height;

            spriteBatch.Draw(bloomTexture, Position - Main.screenPosition, null, Bloom * opacity * 0.5f, 0, bloomTexture.Size() / 2f, Scale * BloomScale * properBloomSize, SpriteEffects.None, 0);
            spriteBatch.Draw(starTexture, Position - Main.screenPosition, null, Color * opacity * 0.5f, Rotation + MathHelper.PiOver4, starTexture.Size() / 2f, Scale * 0.75f, SpriteEffects.None, 0);
            spriteBatch.Draw(starTexture, Position - Main.screenPosition, null, Color * opacity, Rotation, starTexture.Size() / 2f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
