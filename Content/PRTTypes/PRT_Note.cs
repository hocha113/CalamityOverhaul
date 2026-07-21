using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Note : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int noteType;
        private float floatOffset;
        private float pulseTimer;

        public override bool CanPool => true;
        public void Configure(int lifetime, int noteType = -1) {
            Lifetime = lifetime;
            this.noteType = noteType >= 0 ? noteType : Main.rand.Next(3);
            floatOffset = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            noteType = 0;
            floatOffset = 0f;
            pulseTimer = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
        }

        public override void AI() {
            if (Time < 10) {
                Opacity = Time / 10f;
            }
            else if (LifetimeCompletion > 0.7f) {
                Opacity = 1f - (LifetimeCompletion - 0.7f) / 0.3f;
            }
            else {
                Opacity = 1f;
            }

            Rotation = Velocity.ToRotation();

            pulseTimer += 0.1f;
            float verticalWave = MathF.Sin(pulseTimer + floatOffset) * 0.5f;
            Velocity.Y += verticalWave * 0.01f;

            Velocity *= 0.97f;

            float pulseFactor = 1f + MathF.Sin(pulseTimer * 2f) * 0.1f;
            Scale *= 0.995f;
            Scale *= pulseFactor;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            int projType = noteType switch {
                0 => ProjectileID.TiedEighthNote,
                1 => ProjectileID.EighthNote,
                2 => ProjectileID.QuarterNote,
                _ => ProjectileID.EighthNote
            };

            Main.instance.LoadProjectile(projType);
            Texture2D texture = TextureAssets.Projectile[projType].Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Color drawColor = Color * Opacity;

            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                drawColor,
                Rotation,
                texture.Size() / 2f,
                Scale,
                SpriteEffects.None,
                0f
            );

            float glowScale = Scale * 1.2f;
            Color glowColor = Color * Opacity * 0.3f;
            spriteBatch.Draw(
                texture,
                drawPos,
                null,
                glowColor,
                Rotation,
                texture.Size() / 2f,
                glowScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
