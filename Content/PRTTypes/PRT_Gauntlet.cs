using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Gauntlet : BasePRT
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public float SquishStrenght;
        public float MaxSquish;
        public PRT_Gauntlet(Vector2 position, Vector2 velocity, float scale, int lifetime
            , float opacity = 1f, float squishStrenght = 1f, float maxSquish = 3f) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Opacity = opacity;
            Rotation = 0;
            Lifetime = lifetime;
            SquishStrenght = squishStrenght;
            MaxSquish = maxSquish;
        }
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            SetLifetime = true;
        }
        public override void AI() {
            Velocity *= LifetimeCompletion >= 0.34f ? 0.93f : 1.02f;
            Rotation = Velocity.ToRotation();
            Opacity = LifetimeCompletion > 0.5f ?
                (float)Math.Sin(LifetimeCompletion * MathHelper.Pi) * 0.2f + 0.8f
                : (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D value = TexValue;
            Main.EntitySpriteDraw(value, Position - Main.screenPosition, null, Color.Gold * Opacity
                , Rotation + MathHelper.PiOver4 + MathHelper.Pi + (Velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Scale, Velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
