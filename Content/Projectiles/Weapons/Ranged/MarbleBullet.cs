using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class MarbleBullet : GraniteBullet
    {
        private bool onTile;
        public override void AI() {
            CWRDust.SplashDust(Projectile, 6, DustID.WhiteTorch, DustID.WhiteTorch
                , 0, Color.White, EffectsRegistry.StreamerDustShader);
        }

        public override void OnKill(int timeLeft) {
            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRDust.SplashDust(Projectile, 36, DustID.WhiteTorch, DustID.WhiteTorch
                , 10, Color.White, EffectsRegistry.StreamerDustShader);
            if (onTile) {
                float angle = Main.rand.NextFloat(MathF.PI * 2f);
                int numSpikes = 5;
                float spikeAmplitude = 22f;
                float scale = Main.rand.NextFloat(1f, 1.35f);
                for (float spikeAngle = 0f; spikeAngle < MathF.PI * 2f; spikeAngle += 0.05f) {
                    Vector2 offset = spikeAngle.ToRotationVector2() * (2f + 
                        (float)(Math.Sin(angle + spikeAngle * numSpikes) + 1.0) * spikeAmplitude) * 0.15f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, offset, 0, default, scale);
                    dust.shader = EffectsRegistry.StreamerDustShader;
                    dust.noGravity = true;
                    dust.scale += 2;
                }
                Projectile.Explode(160, spanSound: false);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = new Vector2(0, -6);
            Projectile.tileCollide = false;
            Projectile.penetrate = 6;
            onTile = true;
            return false;
        }
    }
}
