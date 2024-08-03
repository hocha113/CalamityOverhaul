using CalamityMod;
using CalamityMod.Projectiles;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class RVoidTentacle : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.MaxUpdates = 3;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.extraUpdates = 1;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Projectile.localAI[1]++;
            if (Projectile.velocity.X != Projectile.velocity.X) {
                if (Math.Abs(Projectile.velocity.X) < 1f) {
                    Projectile.velocity.X = -Projectile.velocity.X;
                }
                else {
                    Projectile.Kill();
                }
            }
            if (Projectile.velocity.Y != Projectile.velocity.Y) {
                if (Math.Abs(Projectile.velocity.Y) < 1f) {
                    Projectile.velocity.Y = -Projectile.velocity.Y;
                }
                else {
                    Projectile.Kill();
                }
            }
            Vector2 center10 = Projectile.Center;
            Projectile.scale = 1f - Projectile.localAI[0];
            Projectile.width = (int)(20f * Projectile.scale);
            Projectile.height = Projectile.width;
            Projectile.position.X = center10.X - Projectile.width / 2;
            Projectile.position.Y = center10.Y - Projectile.height / 2;
            if (Projectile.localAI[0] < 0.1f) {
                Projectile.localAI[0] += 0.01f;
            }
            else {
                Projectile.localAI[0] += 0.025f;
            }
            if (Projectile.localAI[0] >= 0.95f) {
                Projectile.Kill();
            }
            Projectile.velocity.X = Projectile.velocity.X + (Projectile.ai[0] * 1.5f);
            Projectile.velocity.Y = Projectile.velocity.Y + (Projectile.ai[1] * 1.5f);
            if (Projectile.velocity.Length() > 16f) {
                Projectile.velocity.Normalize();
                Projectile.velocity *= 16f;
            }
            Projectile.ai[0] *= 1.05f;
            Projectile.ai[1] *= 1.05f;
            if (Projectile.scale < 2f && Projectile.localAI[1] > 2f) {
                int dustAmount = 0;
                while (dustAmount < Projectile.scale * 5f) {
                    BaseParticle energyLeak = new PRK_Light(Projectile.Center, -Projectile.velocity * 0.05f
                        , Projectile.scale * 0.75f, Color.DarkBlue, 30, 1, 1.5f, hueShift: 0.0f);
                    DRKLoader.AddParticle(energyLeak);
                    dustAmount++;
                }
            }
        }
    }
}
