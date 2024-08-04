using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class EnergyBlast2 : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private bool setDir;
        private Projectile follow => CWRUtils.GetProjectileInstance((int)Projectile.ai[1]);
        private int dir;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12 * Projectile.MaxUpdates;
        }

        public override void AI() {
            if (!setDir) {
                dir = (int)Projectile.ai[0];
                setDir = true;
            }

            if (follow != null) {
                Vector2 pos = follow.Center + follow.velocity.GetNormalVector() * Projectile.ai[2];
                Projectile.Center = Vector2.Lerp(Projectile.Center, pos, 0.13f);
                Projectile.ai[2] += dir * 4;
                if (Math.Abs(Projectile.ai[2]) > 60) {
                    dir *= -1;
                }
            }
            else {
                CalamityUtils.HomeInOnNPC(Projectile, true, 2200f, 12f, 20f);
            }

            if (Projectile.Center.To(Main.player[Projectile.owner].Center).LengthSquared() <= 1500 * 1500) {
                DRK_Light particle = new DRK_Light(Projectile.Center, Vector2.Zero, 0.2f, Main.DiscoColor, 22);
                DRKLoader.AddParticle(particle);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 60);
            if (Projectile.numHits == 0)
                EnergyBlast.SpanDust(Projectile);
        }
    }
}
