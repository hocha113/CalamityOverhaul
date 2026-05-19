using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Bloomlight : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Photosphere";
        public override bool CanPool => true;
        public PRT_Bloomlight Configure(int lt, bool produceLight = true, bool additiveBlend = true) {
            Lifetime = lt;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
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
