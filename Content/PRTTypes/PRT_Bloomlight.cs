using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Bloomlight : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Photosphere";
        public PRT_Bloomlight(Vector2 position, Vector2 velocity, Color color, float scale, int lifeTime, bool produceLight = true, bool AddativeBlend = true) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = scale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void AI() {
            Opacity = (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, null, Color * Opacity, Rotation, TexValue.Size() / 2f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
