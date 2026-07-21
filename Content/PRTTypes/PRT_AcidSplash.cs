using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>酸液飞溅</summary>
    internal class PRT_AcidSplash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private Color acidColor;
        private bool affectedByGravity;
        private float stretchFactor;

        public override bool CanPool => true;
        public PRT_AcidSplash() {
            affectedByGravity = true;
            acidColor = new Color(110, 200, 120);
        }
        public PRT_AcidSplash(Vector2 position, Vector2 velocity, float scale, int lifetime, bool gravity = true) {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = lifetime;
            affectedByGravity = gravity;

            acidColor = Main.rand.Next(4) switch {
                0 => new Color(110, 200, 120),
                1 => new Color(90, 180, 100),
                2 => new Color(130, 220, 140),
                _ => new Color(100, 190, 110)
            };
        }
        public PRT_AcidSplash Configure(int lt, bool gravity = true) {
            Lifetime = lt;
            affectedByGravity = gravity;
            acidColor = Main.rand.Next(4) switch {
                0 => new Color(110, 200, 120),
                1 => new Color(90, 180, 100),
                2 => new Color(130, 220, 140),
                _ => new Color(100, 190, 110)
            };
            return this;
        }
        public override void Reset() {
            base.Reset();
            acidColor = new Color(110, 200, 120);
            affectedByGravity = true;
            stretchFactor = 0f;
        }

        public override void SetProperty() {
            Opacity = 1f;
        }

        public override void AI() {
            Color = Color.Lerp(acidColor, Color.Green, LifetimeCompletion);

            Opacity = 1f - (float)Math.Pow(LifetimeCompletion, 2);

            if (affectedByGravity) {
                Velocity.Y += 0.15f;
                Velocity.X *= 0.98f;
            }
            else {
                Velocity *= 0.96f;
            }

            stretchFactor = MathHelper.Clamp(Velocity.Length() / 5f, 0.5f, 3f);

            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            Scale *= 0.97f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            Vector2 scaleVec = new Vector2(Scale * 0.6f, Scale * stretchFactor * 1.8f);

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color * Opacity,
                Rotation,
                origin,
                scaleVec,
                SpriteEffects.None,
                0f
            );

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                Color * Opacity * 0.5f,
                Rotation,
                origin,
                scaleVec * new Vector2(0.7f, 1.1f),
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
